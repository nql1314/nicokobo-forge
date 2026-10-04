param(
    [string]$ForgeDll = '',
    [string]$ModDistRoot = ''
)

$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
$useLocalBuild = [string]::IsNullOrWhiteSpace($ForgeDll)
if ($useLocalBuild) {
    $ForgeDll = Join-Path $projectRoot 'src\Nicokobo.Forge\bin\Release\Nicokobo.Forge.dll'
}
if (-not (Test-Path -LiteralPath $ForgeDll -PathType Leaf)) {
    throw "Forge DLL missing; build Forge first or pass -ForgeDll: $ForgeDll"
}
$sourceDll = (Resolve-Path -LiteralPath $ForgeDll).Path
$assembly = [Reflection.AssemblyName]::GetAssemblyName($sourceDll)
if ($assembly.Name -ne 'Nicokobo.Forge') { throw "Expected Nicokobo.Forge assembly: $sourceDll" }
$assemblyVersion = $assembly.Version
$version = "$($assemblyVersion.Major).$($assemblyVersion.Minor).$($assemblyVersion.Build)"
if ($version -notmatch '^\d+\.\d+\.\d+$') { throw "Unexpected Forge version: $assemblyVersion" }
if ($useLocalBuild) {
    $projectFile = Join-Path $projectRoot 'src\Nicokobo.Forge\Nicokobo.Forge.csproj'
    $project = [xml](Get-Content -LiteralPath $projectFile -Raw)
    $projectVersion = [string]$project.Project.PropertyGroup.Version
    if ($projectVersion -notmatch '^\d+\.\d+\.\d+$') { throw "Unexpected project version: $projectVersion" }
    if ($version -ne $projectVersion) {
        throw "DLL version $assemblyVersion does not match project version $projectVersion"
    }
}

$distDllName = "Nicokobo.Forge-$version.dll"

$distRoot = Join-Path $projectRoot 'dist'
$packageRoot = Join-Path $distRoot 'nicokobo-forge'
$archivePath = Join-Path $distRoot 'nicokobo-forge.zip'
$expectedDistRoot = [IO.Path]::GetFullPath($distRoot)
if (-not $expectedDistRoot.StartsWith($projectRoot + [IO.Path]::DirectorySeparatorChar)) {
    throw "Unsafe distribution path: $expectedDistRoot"
}
$readmeTemplate = Join-Path $projectRoot 'docs\RELEASE_README.md'
if (-not (Test-Path -LiteralPath $readmeTemplate -PathType Leaf)) {
    throw "Document missing: $readmeTemplate"
}
$readmeText = [IO.File]::ReadAllText($readmeTemplate).Replace('@FORGE_VERSION@', $version)
New-Item -ItemType Directory -Path $packageRoot -Force | Out-Null
$packageDirectory = Get-Item -LiteralPath $packageRoot
if ($packageDirectory.Parent.FullName -ne $expectedDistRoot -or
    ($packageDirectory.Attributes -band [IO.FileAttributes]::ReparsePoint)) {
    throw "Unsafe package directory: $packageRoot"
}
[IO.File]::WriteAllText((Join-Path $packageRoot 'README.md'), $readmeText,
    [Text.UTF8Encoding]::new($false))
$destinationDll = Join-Path $packageRoot $distDllName
if ($sourceDll -ne $destinationDll) {
    Copy-Item -LiteralPath $sourceDll -Destination $destinationDll -Force
}
Get-ChildItem -LiteralPath $packageRoot -File -Filter 'Nicokobo.Forge*.dll' |
    Where-Object { $_.Name -ne $distDllName } |
    ForEach-Object { Remove-Item -LiteralPath $_.FullName -Force }

# Include the maintained package documents, keeping the nicokobo-forge/ ZIP root.
Compress-Archive -LiteralPath $packageRoot -DestinationPath $archivePath -CompressionLevel Optimal -Force

if ([string]::IsNullOrWhiteSpace($ModDistRoot)) {
    $ModDistRoot = Join-Path $projectRoot '..\probably-stolen\mods-melonloader\dist'
}
$modDistRoot = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($ModDistRoot)
# [IO.Path]::TrimEndingDirectorySeparator is .NET Core 2.1+ only, so Windows PowerShell 5.1 cannot call it.
if ($modDistRoot -ne [IO.Path]::GetPathRoot($modDistRoot)) {
    $modDistRoot = $modDistRoot.TrimEnd([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar)
}
if ($modDistRoot -ne $expectedDistRoot) {
    $modPackageRoot = Join-Path $modDistRoot 'nicokobo-forge'
    New-Item -ItemType Directory -Path $modPackageRoot -Force | Out-Null
    $modPackageDirectory = Get-Item -LiteralPath $modPackageRoot
    if ($modPackageDirectory.Parent.FullName -ne $modDistRoot -or
        ($modPackageDirectory.Attributes -band [IO.FileAttributes]::ReparsePoint)) {
        throw "Unsafe Mod package directory: $modPackageRoot"
    }
    foreach ($file in Get-ChildItem -LiteralPath $packageRoot -File) {
        Copy-Item -LiteralPath $file.FullName -Destination (Join-Path $modPackageRoot $file.Name) -Force
    }
    Get-ChildItem -LiteralPath $modPackageRoot -File -Filter 'Nicokobo.Forge*.dll' |
        Where-Object { $_.Name -ne $distDllName } |
        ForEach-Object { Remove-Item -LiteralPath $_.FullName -Force }
    $modArchivePath = Join-Path $modDistRoot 'nicokobo-forge.zip'
    Copy-Item -LiteralPath $archivePath -Destination $modArchivePath -Force
    Write-Output "Mod package directory: $modPackageRoot"
    Write-Output "Mod archive: $modArchivePath"
}

Write-Output "Package directory: $packageRoot"
Write-Output "Website archive: $archivePath"
Write-Output "Forge version: $version"
