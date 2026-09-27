param()

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$packageRoot = Join-Path $projectRoot 'dist\p0'
$expectedPackageRoot = [IO.Path]::GetFullPath((Join-Path $projectRoot 'dist\p0'))
if ([IO.Path]::GetFullPath($packageRoot) -ne $expectedPackageRoot -or
    -not $expectedPackageRoot.StartsWith($projectRoot + [IO.Path]::DirectorySeparatorChar)) {
    throw "Unsafe package path: $packageRoot"
}
New-Item -ItemType Directory -Path $packageRoot -Force | Out-Null
Get-ChildItem -LiteralPath $packageRoot -File | Remove-Item -Force
$artifacts = @(
    'src\Nicokobo.Forge\bin\Release\Nicokobo.Forge.dll',
    'samples\Nicokobo.Forge.ExampleOne\bin\Release\Nicokobo.Forge.ExampleOne.dll',
    'samples\Nicokobo.Forge.ExampleTwo\bin\Release\Nicokobo.Forge.ExampleTwo.dll'
)
$manifest = foreach ($artifact in $artifacts) {
    $source = Join-Path $projectRoot $artifact
    if (-not (Test-Path -LiteralPath $source)) { throw "Build artifact missing: $source" }
    $destination = Join-Path $packageRoot (Split-Path -Leaf $source)
    Copy-Item -LiteralPath $source -Destination $destination -Force
    [pscustomobject]@{
        file = Split-Path -Leaf $destination
        sha256 = (Get-FileHash -LiteralPath $destination -Algorithm SHA256).Hash
    }
}
$manifest | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $packageRoot 'manifest.json') -Encoding UTF8
Write-Output $packageRoot
