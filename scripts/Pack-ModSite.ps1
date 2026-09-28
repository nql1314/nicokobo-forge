param()

$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
$projectFile = Join-Path $projectRoot 'src\Nicokobo.Forge\Nicokobo.Forge.csproj'
$project = [xml](Get-Content -LiteralPath $projectFile -Raw)
$version = [string]$project.Project.PropertyGroup.Version
if ($version -notmatch '^\d+\.\d+\.\d+$') { throw "Unexpected Forge version: $version" }

$sourceDll = Join-Path $projectRoot 'src\Nicokobo.Forge\bin\Release\Nicokobo.Forge.dll'
if (-not (Test-Path -LiteralPath $sourceDll -PathType Leaf)) {
    throw "Release DLL missing; build Forge first: $sourceDll"
}
$assemblyVersion = [Reflection.AssemblyName]::GetAssemblyName($sourceDll).Version
if ($assemblyVersion.Major -ne ([version]$version).Major -or
    $assemblyVersion.Minor -ne ([version]$version).Minor -or
    $assemblyVersion.Build -ne ([version]$version).Build) {
    throw "DLL version $assemblyVersion does not match project version $version"
}

$distDllName = "Nicokobo.Forge-$version.dll"

$distRoot = Join-Path $projectRoot 'dist'
$packageRoot = Join-Path $distRoot 'nicokobo-forge'
$archivePath = Join-Path $distRoot "Nicokobo.Forge-$version.zip"
$expectedDistRoot = [IO.Path]::GetFullPath($distRoot)
if (-not $expectedDistRoot.StartsWith($projectRoot + [IO.Path]::DirectorySeparatorChar)) {
    throw "Unsafe distribution path: $expectedDistRoot"
}
New-Item -ItemType Directory -Path $packageRoot -Force | Out-Null
Get-ChildItem -LiteralPath $packageRoot -File -Filter 'Nicokobo.Forge*.dll' | Remove-Item -Force

foreach ($name in @('README.md')) {
    $source = Join-Path $projectRoot $name
    if (-not (Test-Path -LiteralPath $source -PathType Leaf)) { throw "Document missing: $source" }
    Copy-Item -LiteralPath $source -Destination (Join-Path $packageRoot $name) -Force
}
Copy-Item -LiteralPath $sourceDll -Destination (Join-Path $packageRoot $distDllName) -Force
$cover = Join-Path $projectRoot 'cover.png'
if (Test-Path -LiteralPath $cover -PathType Leaf) {
    Copy-Item -LiteralPath $cover -Destination (Join-Path $packageRoot 'cover.png') -Force
}

$packageFiles = @($distDllName, 'README.md') |
    ForEach-Object { Join-Path $packageRoot $_ }
Compress-Archive -LiteralPath $packageFiles -DestinationPath $archivePath -CompressionLevel Optimal -Force

Write-Output "Package directory: $packageRoot"
Write-Output "Website archive: $archivePath"
Write-Output "Forge SHA-256: $((Get-FileHash -LiteralPath (Join-Path $packageRoot $distDllName) -Algorithm SHA256).Hash)"
