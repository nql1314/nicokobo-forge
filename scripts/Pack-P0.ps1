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
    @{ Project = 'src\Nicokobo.Forge\Nicokobo.Forge.csproj'; Dll = 'src\Nicokobo.Forge\bin\Release\Nicokobo.Forge.dll' },
    @{ Project = 'samples\Nicokobo.Forge.ExampleOne\Nicokobo.Forge.ExampleOne.csproj'; Dll = 'samples\Nicokobo.Forge.ExampleOne\bin\Release\Nicokobo.Forge.ExampleOne.dll' },
    @{ Project = 'samples\Nicokobo.Forge.ExampleTwo\Nicokobo.Forge.ExampleTwo.csproj'; Dll = 'samples\Nicokobo.Forge.ExampleTwo\bin\Release\Nicokobo.Forge.ExampleTwo.dll' }
)
$manifest = foreach ($artifact in $artifacts) {
    $source = Join-Path $projectRoot $artifact.Dll
    if (-not (Test-Path -LiteralPath $source)) { throw "Build artifact missing: $source" }
    $projectFile = Join-Path $projectRoot $artifact.Project
    if (-not (Test-Path -LiteralPath $projectFile -PathType Leaf)) { throw "Project missing: $projectFile" }
    $project = [xml](Get-Content -LiteralPath $projectFile -Raw)
    $version = [string]$project.Project.PropertyGroup.Version
    if ($version -notmatch '^\d+\.\d+\.\d+$') { throw "Unexpected version in $($artifact.Project): $version" }
    $assemblyVersion = [Reflection.AssemblyName]::GetAssemblyName($source).Version
    if ($assemblyVersion.Major -ne ([version]$version).Major -or
        $assemblyVersion.Minor -ne ([version]$version).Minor -or
        $assemblyVersion.Build -ne ([version]$version).Build) {
        throw "DLL version $assemblyVersion does not match project version $version ($($artifact.Project))"
    }
    $name = "$([IO.Path]::GetFileNameWithoutExtension($source))-$version.dll"
    $destination = Join-Path $packageRoot $name
    Copy-Item -LiteralPath $source -Destination $destination -Force
    [pscustomobject]@{
        file = $name
        sha256 = (Get-FileHash -LiteralPath $destination -Algorithm SHA256).Hash
    }
}
$manifest | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $packageRoot 'manifest.json') -Encoding UTF8
& (Join-Path $PSScriptRoot 'Pack-ModSite.ps1')
Write-Output $packageRoot
