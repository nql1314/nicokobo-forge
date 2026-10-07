param(
    [string]$ForgeDll = '',
    [string]$ModDistRoot = '',
    [string]$CompatibilityDll = ''
)

$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))

# Editors, scanners and the running game can briefly keep a memory-mapped section open on
# package files, which makes Copy-Item fail even though the content is identical. Skip
# unchanged files and retry the remaining writes so packaging survives those transient locks.
function Copy-PackageFile([string]$From, [string]$To) {
    if (Test-Path -LiteralPath $To -PathType Leaf) {
        $incoming = Get-Item -LiteralPath $From
        $existing = Get-Item -LiteralPath $To
        if ($existing.Length -eq $incoming.Length -and
            (Get-FileHash -LiteralPath $From -Algorithm SHA256).Hash -eq
            (Get-FileHash -LiteralPath $To -Algorithm SHA256).Hash) {
            return
        }
    }
    for ($attempt = 1; ; $attempt++) {
        try {
            Copy-Item -LiteralPath $From -Destination $To -Force -ErrorAction Stop
            return
        }
        catch {
            if ($attempt -ge 5) { throw }
            Start-Sleep -Milliseconds 200
        }
    }
}

function Write-PackageDocument([string]$Path, [string]$Text) {
    if (Test-Path -LiteralPath $Path -PathType Leaf) {
        if ([IO.File]::ReadAllText($Path) -ceq $Text) { return }
    }
    $encoding = [Text.UTF8Encoding]::new($false)
    for ($attempt = 1; ; $attempt++) {
        try {
            [IO.File]::WriteAllText($Path, $Text, $encoding)
            return
        }
        catch {
            if ($attempt -ge 5) { throw }
            Start-Sleep -Milliseconds 200
        }
    }
}

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

# The optional compatibility patches travel with the Forge package so players get them
# from one download. Forge never installs or loads the DLL by itself; the release copy
# under dist/compatibility is the default source, and a local Release build is used
# only when no packaged copy exists yet.
if ([string]::IsNullOrWhiteSpace($CompatibilityDll)) {
    $compatibilityPackageRoot = Join-Path $distRoot 'compatibility\nicokobo-compatibility-patches'
    $packagedCompatibility = @()
    if (Test-Path -LiteralPath $compatibilityPackageRoot -PathType Container) {
        $packagedCompatibility = @(Get-ChildItem -LiteralPath $compatibilityPackageRoot -File -Filter 'Nicokobo.CompatibilityPatches-*.dll' | Sort-Object Name)
    }
    if ($packagedCompatibility.Count -gt 0) {
        $CompatibilityDll = $packagedCompatibility[-1].FullName
    }
    else {
        $CompatibilityDll = Join-Path $projectRoot 'compatibility\Nicokobo.CompatibilityPatches\bin\Release\Nicokobo.CompatibilityPatches.dll'
    }
}
$compatibilityFileName = ''
$compatibilitySourceDll = ''
if (Test-Path -LiteralPath $CompatibilityDll -PathType Leaf) {
    $compatibilitySourceDll = (Resolve-Path -LiteralPath $CompatibilityDll).Path
    $compatibilityAssembly = [Reflection.AssemblyName]::GetAssemblyName($compatibilitySourceDll)
    if ($compatibilityAssembly.Name -ne 'Nicokobo.CompatibilityPatches') {
        throw "Expected a Nicokobo.CompatibilityPatches assembly: $compatibilitySourceDll"
    }
    $compatibilityVersion = "$($compatibilityAssembly.Version.Major).$($compatibilityAssembly.Version.Minor).$($compatibilityAssembly.Version.Build)"
    if ($compatibilityVersion -notmatch '^\d+\.\d+\.\d+$') {
        throw "Unexpected compatibility version in $($compatibilitySourceDll): $($compatibilityAssembly.Version)"
    }
    $compatibilityFileName = "Nicokobo.CompatibilityPatches-$compatibilityVersion.dll"
}
else {
    Write-Warning "Optional compatibility patches missing; the Forge package is written without them: $CompatibilityDll"
}

$readmeTemplate = Join-Path $projectRoot 'docs\RELEASE_README.md'
if (-not (Test-Path -LiteralPath $readmeTemplate -PathType Leaf)) {
    throw "Document missing: $readmeTemplate"
}
$readmeText = [IO.File]::ReadAllText($readmeTemplate).Replace('@FORGE_VERSION@', $version).Replace('[CHANGELOG](RELEASE_CHANGELOG.md)', '[change.log](change.log)')
if ([string]::IsNullOrEmpty($compatibilityFileName)) {
    # No bundled DLL means the generated README must not advertise an optional component.
    $readmeText = [regex]::Replace($readmeText, '(?s)@COMPAT_SECTION_BEGIN@.*?@COMPAT_SECTION_END@\r?\n\r?\n\r?\n', '')
    $readmeText = [regex]::Replace($readmeText, '(?s)@COMPAT_SECTION_BEGIN_EN@.*?@COMPAT_SECTION_END_EN@\r?\n\r?\n\r?\n', '')
}
else {
    $readmeText = $readmeText.Replace('@COMPAT_DLL@', $compatibilityFileName)
    $readmeText = [regex]::Replace($readmeText, '(?m)^@COMPAT_SECTION_BEGIN@\r?\n', '')
    $readmeText = [regex]::Replace($readmeText, '(?m)^@COMPAT_SECTION_END@\r?\n', '')
    $readmeText = [regex]::Replace($readmeText, '(?m)^@COMPAT_SECTION_BEGIN_EN@\r?\n', '')
    $readmeText = [regex]::Replace($readmeText, '(?m)^@COMPAT_SECTION_END_EN@\r?\n', '')
}
New-Item -ItemType Directory -Path $packageRoot -Force | Out-Null
$packageDirectory = Get-Item -LiteralPath $packageRoot
if ($packageDirectory.Parent.FullName -ne $expectedDistRoot -or
    ($packageDirectory.Attributes -band [IO.FileAttributes]::ReparsePoint)) {
    throw "Unsafe package directory: $packageRoot"
}
Write-PackageDocument -Path (Join-Path $packageRoot 'README.md') -Text $readmeText
Copy-PackageFile -From (Join-Path $projectRoot 'docs\RELEASE_CHANGELOG.md') `
    -To (Join-Path $packageRoot 'change.log')
# Earlier packages shipped the same document as CHANGELOG.md; drop that name so the
# release folder and its archive keep a single changelog document.
Remove-Item -LiteralPath (Join-Path $packageRoot 'CHANGELOG.md') -Force -ErrorAction SilentlyContinue
$destinationDll = Join-Path $packageRoot $distDllName
if ($sourceDll -ne $destinationDll) {
    Copy-PackageFile -From $sourceDll -To $destinationDll
}
Get-ChildItem -LiteralPath $packageRoot -File -Filter 'Nicokobo.Forge*.dll' |
    Where-Object { $_.Name -ne $distDllName } |
    ForEach-Object { Remove-Item -LiteralPath $_.FullName -Force }
# Optional compatibility patches: bundled but never loaded by Forge, and cleared when absent.
Get-ChildItem -LiteralPath $packageRoot -File -Filter 'Nicokobo.CompatibilityPatches*.dll' |
    Where-Object { [string]::IsNullOrEmpty($compatibilityFileName) -or $_.Name -ne $compatibilityFileName } |
    ForEach-Object { Remove-Item -LiteralPath $_.FullName -Force }
if (-not [string]::IsNullOrEmpty($compatibilityFileName)) {
    Copy-PackageFile -From $compatibilitySourceDll -To (Join-Path $packageRoot $compatibilityFileName)
}

# Include the maintained package documents, keeping the nicokobo-forge/ ZIP root.
for ($attempt = 1; ; $attempt++) {
    try {
        Compress-Archive -LiteralPath $packageRoot -DestinationPath $archivePath `
            -CompressionLevel Optimal -Force -ErrorAction Stop
        break
    }
    catch {
        if ($attempt -ge 5) { throw }
        Start-Sleep -Milliseconds 200
    }
}

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
        Copy-PackageFile -From $file.FullName -To (Join-Path $modPackageRoot $file.Name)
    }
    Remove-Item -LiteralPath (Join-Path $modPackageRoot 'CHANGELOG.md') -Force -ErrorAction SilentlyContinue
    Get-ChildItem -LiteralPath $modPackageRoot -File -Filter 'Nicokobo.Forge*.dll' |
        Where-Object { $_.Name -ne $distDllName } |
        ForEach-Object { Remove-Item -LiteralPath $_.FullName -Force }
    Get-ChildItem -LiteralPath $modPackageRoot -File -Filter 'Nicokobo.CompatibilityPatches*.dll' |
        Where-Object { [string]::IsNullOrEmpty($compatibilityFileName) -or $_.Name -ne $compatibilityFileName } |
        ForEach-Object { Remove-Item -LiteralPath $_.FullName -Force }
    $modArchivePath = Join-Path $modDistRoot 'nicokobo-forge.zip'
    Copy-PackageFile -From $archivePath -To $modArchivePath
    Write-Output "Mod package directory: $modPackageRoot"
    Write-Output "Mod archive: $modArchivePath"
}

Write-Output "Package directory: $packageRoot"
Write-Output "Website archive: $archivePath"
Write-Output "Forge version: $version"
if ([string]::IsNullOrEmpty($compatibilityFileName)) {
    Write-Output 'Optional compatibility patches: not bundled'
}
else {
    Write-Output "Optional compatibility patches: $compatibilityFileName"
}
