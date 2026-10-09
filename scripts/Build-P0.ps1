param(
    [Parameter(Mandatory = $true)]
    [string]$GameDir,
    [string]$DefaultLogLevel = $env:ModDefaultLogLevel,
    [switch]$IncludeProbes
)

$ErrorActionPreference = 'Stop'
$DefaultLogLevel = if ([string]::IsNullOrWhiteSpace($DefaultLogLevel)) {
    'INFO'
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

& (Join-Path $PSScriptRoot 'Check-AdapterContracts.ps1') -GameDir $resolvedGameDir
& (Join-Path $PSScriptRoot 'Check-InventoryPowerContracts.ps1') -GameDir $resolvedGameDir

& dotnet run --project (Join-Path $projectRoot 'tests\Nicokobo.Forge.Domain.Check\Nicokobo.Forge.Domain.Check.csproj') -c Release -p:NuGetAudit=false
if ($LASTEXITCODE -ne 0) { throw 'Domain checks failed' }

& dotnet run --project (Join-Path $projectRoot 'tests\Nicokobo.Forge.Lifecycle.Check\Nicokobo.Forge.Lifecycle.Check.csproj') -c Release -p:NuGetAudit=false
if ($LASTEXITCODE -ne 0) { throw 'Lifecycle checks failed' }

& dotnet run --project (Join-Path $projectRoot 'tests\Nicokobo.Forge.HookGuard.Check\Nicokobo.Forge.HookGuard.Check.csproj') `
    -c Release "-p:GameDir=$resolvedGameDir" -p:NuGetAudit=false
if ($LASTEXITCODE -ne 0) { throw 'Native hook entry-point guard checks failed' }

& dotnet msbuild (Join-Path $PSScriptRoot 'Forge.Build.proj') -t:BuildAll -nologo -v:minimal `
    "-p:GameDir=$resolvedGameDir" "-p:ModDefaultLogLevel=$DefaultLogLevel" `
    "-p:IncludeProbes=$($IncludeProbes.IsPresent.ToString().ToLowerInvariant())"
if ($LASTEXITCODE -ne 0) { throw 'Forge or sample build failed' }
