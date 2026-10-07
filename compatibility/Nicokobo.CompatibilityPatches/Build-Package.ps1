param([Parameter(Mandatory = $true)][string]$GameDir)

$ErrorActionPreference = 'Stop'
$project = Join-Path $PSScriptRoot 'Nicokobo.CompatibilityPatches.csproj'
$checks = Join-Path $PSScriptRoot 'Checks\Checks.csproj'
& dotnet build $project -c Release "-p:GameDir=$GameDir"
if ($LASTEXITCODE -ne 0) { throw 'Compatibility build failed' }
$binary = Join-Path $PSScriptRoot 'bin\Release\Nicokobo.CompatibilityPatches.dll'
& dotnet run --project $checks -c Release "-p:GameDir=$GameDir" -- $GameDir $binary
if ($LASTEXITCODE -ne 0) { throw 'Compatibility checks failed' }
$repoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$packageRoot = Join-Path $repoRoot 'dist\compatibility\nicokobo-compatibility-patches'
New-Item -ItemType Directory -Path $packageRoot -Force | Out-Null
$projectXml = [xml](Get-Content -LiteralPath $project -Raw)
$version = [string]$projectXml.Project.PropertyGroup.Version
$assemblyName = [string]$projectXml.Project.PropertyGroup.AssemblyName
Copy-Item -LiteralPath (Join-Path $PSScriptRoot "bin\Release\$assemblyName.dll") -Destination (Join-Path $packageRoot "$assemblyName-$version.dll") -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'README.md') -Destination (Join-Path $packageRoot 'README.md') -Force
Get-ChildItem -LiteralPath $packageRoot -Filter "$assemblyName-*.dll" -File |
    Where-Object Name -ne "$assemblyName-$version.dll" |
    ForEach-Object { Remove-Item -LiteralPath $_.FullName }
Write-Output $packageRoot
