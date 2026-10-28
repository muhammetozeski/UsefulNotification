#Requires -Version 7.0
<#
.SYNOPSIS
Builds the Core NuGet package and an offline SDK containing its resolved dependencies.
.PARAMETER OutputDirectory
Empty destination for Packages, Install.ps1, README.md, and a minimal consumer example.
.PARAMETER WorkspaceDirectory
Build and restore intermediates; defaults to a sibling of OutputDirectory.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$OutputDirectory,
    [string]$WorkspaceDirectory
)

$ErrorActionPreference = 'Stop'
$repository = Split-Path $PSScriptRoot -Parent
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
if (!$WorkspaceDirectory) { $WorkspaceDirectory = "$OutputDirectory.build" }
$WorkspaceDirectory = [IO.Path]::GetFullPath($WorkspaceDirectory)
if ((Test-Path -LiteralPath $OutputDirectory) -and
    (Get-ChildItem -LiteralPath $OutputDirectory -Force | Select-Object -First 1)) {
    throw "OutputDirectory must be empty: $OutputDirectory"
}
if ($WorkspaceDirectory.StartsWith($OutputDirectory + [IO.Path]::DirectorySeparatorChar,
        [StringComparison]::OrdinalIgnoreCase) -or $WorkspaceDirectory -eq $OutputDirectory) {
    throw 'Keep WorkspaceDirectory outside the distributable OutputDirectory.'
}

$packages = Join-Path $OutputDirectory 'Packages'
$consumer = Join-Path $WorkspaceDirectory 'Consumer'
New-Item -ItemType Directory -Path $packages, $consumer -Force | Out-Null
$project = Join-Path $repository 'src\UsefulNotification.Core\UsefulNotification.Core.csproj'
Write-Host "Packing Core into $packages"
& dotnet pack $project --configuration Release --output $packages `
    --artifacts-path (Join-Path $WorkspaceDirectory 'Artifacts') --nologo
if ($LASTEXITCODE) { throw "Core packing failed ($LASTEXITCODE)." }

Add-Type -AssemblyName System.IO.Compression.FileSystem
$corePackages = @(Get-ChildItem -LiteralPath $packages -Filter 'UsefulNotification.Core.*.nupkg' |
    Where-Object Name -NotLike '*.symbols.nupkg')
if ($corePackages.Count -ne 1) { throw 'Expected exactly one Core package.' }
$corePackage = $corePackages[0]
$archive = [IO.Compression.ZipFile]::OpenRead($corePackage.FullName)
try {
    $reader = [IO.StreamReader]::new(($archive.Entries | Where-Object FullName -Like '*.nuspec').Open())
    try { [xml]$specification = $reader.ReadToEnd() } finally { $reader.Dispose() }
    $version = [string]$specification.package.metadata.version
} finally { $archive.Dispose() }

# Resolve a real consumer so private build tools never enter the offline runtime feed.
$consumerProject = Join-Path $consumer 'Consumer.csproj'
@"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup><TargetFramework>net10.0</TargetFramework><PlatformTarget>x64</PlatformTarget><NuGetAudit>false</NuGetAudit></PropertyGroup>
  <ItemGroup><PackageReference Include="UsefulNotification.Core" Version="[$version]" /></ItemGroup>
</Project>
"@ | Set-Content -LiteralPath $consumerProject -Encoding utf8
& dotnet restore $consumerProject --packages (Join-Path $WorkspaceDirectory 'ConsumerPackages') `
    --property:"RestoreAdditionalProjectSources=$packages" --nologo
if ($LASTEXITCODE) { throw "Consumer dependency restore failed ($LASTEXITCODE)." }
$assets = Get-Content -LiteralPath (Join-Path $consumer 'obj\project.assets.json') -Raw | ConvertFrom-Json
$inventory = foreach ($library in $assets.libraries.PSObject.Properties | Sort-Object Name) {
    if ($library.Value.type -ne 'package') { continue }
    $identity = $library.Name.Split('/')
    $packageName = "$($identity[0].ToLowerInvariant()).$($identity[1]).nupkg"
    if ($identity[0] -eq 'UsefulNotification.Core') {
        $packageFile = $corePackage.FullName
    } else {
        $packageFile = $null
        foreach ($folder in $assets.packageFolders.PSObject.Properties.Name) {
            $candidate = Join-Path (Join-Path $folder $library.Value.path) $packageName
            if (Test-Path -LiteralPath $candidate -PathType Leaf) { $packageFile = $candidate; break }
        }
        if (!$packageFile) { throw "NuGet did not retain the resolved archive: $($library.Name)" }
        Copy-Item -LiteralPath $packageFile -Destination $packages
        $packageFile = Join-Path $packages $packageName
    }

    $archive = [IO.Compression.ZipFile]::OpenRead($packageFile)
    try {
        $requiredFiles = @($archive.Entries | Where-Object {
            $_.Name -and $_.FullName -notmatch '^(?:_rels/|package/|\[Content_Types\]\.xml$|\.signature\.p7s$)'
        } | ForEach-Object {
            if ($_.FullName -like '*.nuspec') { $_.FullName.ToLowerInvariant() } else { $_.FullName }
        })
    } finally { $archive.Dispose() }
    $hash = [Convert]::ToBase64String([Security.Cryptography.SHA512]::HashData([IO.File]::ReadAllBytes($packageFile)))
    Write-Host "Bundled $($library.Name)"
    [ordered]@{
        id = $identity[0]; version = $identity[1]; archive = [IO.Path]::GetFileName($packageFile)
        sha512 = $hash; files = $requiredFiles
    }
}
[ordered]@{ version = $version; targetFramework = 'net10.0'; packages = @($inventory) } |
    ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $OutputDirectory 'Sdk.json') -Encoding utf8
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Install.ps1'), (Join-Path $PSScriptRoot 'README.md') -Destination $OutputDirectory
Copy-Item -LiteralPath (Join-Path $repository 'LICENSE') -Destination $OutputDirectory
$example = Join-Path $OutputDirectory 'Example'
New-Item -ItemType Directory -Path $example | Out-Null
@"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><PlatformTarget>x64</PlatformTarget><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable></PropertyGroup>
  <ItemGroup><PackageReference Include="UsefulNotification.Core" Version="$version" /></ItemGroup>
</Project>
"@ | Set-Content -LiteralPath (Join-Path $example 'Example.csproj') -Encoding utf8
@'
using UsefulNotification;

NotificationReceipt receipt = await NotificationClient.SendAsync(new()
{
    Title = "Backup complete",
    Message = "All files were copied.",
    Theme = NotificationThemes.Happy,
    Sound = NotificationSounds.Subtle
});
Console.WriteLine(receipt.Accepted ? "Accepted" : receipt.Failure?.Message);
'@ | Set-Content -LiteralPath (Join-Path $example 'Program.cs') -Encoding utf8
Write-Host "Offline SDK $version is ready: $OutputDirectory"
