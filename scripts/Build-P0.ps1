param(
    [Parameter(Mandatory = $true)]
    [string]$GameDir,
    [string]$DefaultLogLevel = $env:ModDefaultLogLevel
)

$ErrorActionPreference = 'Stop'
$DefaultLogLevel = if ([string]::IsNullOrWhiteSpace($DefaultLogLevel)) {
    'WARN'
} else {
    $DefaultLogLevel.Trim().ToUpperInvariant()
}
if ($DefaultLogLevel -notin @('DEBUG', 'INFO', 'WARN', 'ERROR')) {
    throw 'DefaultLogLevel must be DEBUG, INFO, WARN, or ERROR.'
}
$projectRoot = Split-Path -Parent $PSScriptRoot
$resolvedGameDir = (Resolve-Path -LiteralPath $GameDir).Path
if (-not (Test-Path -LiteralPath (Join-Path $resolvedGameDir 'GameAssembly.dll'))) {
    throw "GameAssembly.dll not found under $resolvedGameDir"
}

& dotnet run --project (Join-Path $projectRoot 'tests\Nicokobo.Forge.Domain.Check\Nicokobo.Forge.Domain.Check.csproj') -c Release
if ($LASTEXITCODE -ne 0) { throw 'Domain checks failed' }

foreach ($project in @(
    'src\Nicokobo.Forge\Nicokobo.Forge.csproj',
    'samples\Nicokobo.Forge.ExampleOne\Nicokobo.Forge.ExampleOne.csproj',
    'samples\Nicokobo.Forge.ExampleTwo\Nicokobo.Forge.ExampleTwo.csproj',
    'samples\Nicokobo.Forge.LogisticsExtension\Nicokobo.Forge.LogisticsExtension.csproj'
)) {
    & dotnet build (Join-Path $projectRoot $project) -c Release "-p:GameDir=$resolvedGameDir" "-p:ModDefaultLogLevel=$DefaultLogLevel" -v minimal
    if ($LASTEXITCODE -ne 0) { throw "Build failed: $project" }
}
