#Requires -Version 5.1
<#
.SYNOPSIS
Registers the SDK's Packages folder once and restores missing package versions offline.
.PARAMETER UserConfig
Optional user NuGet.Config path, mainly for isolated installation checks.
.PARAMETER PackagesPath
Optional global-packages override, mainly for isolated installation checks.
.PARAMETER WorkspaceDirectory
Disposable restore files; defaults to AppCache beside this installer.
#>
[CmdletBinding()]
param(
    [string]$UserConfig,
    [string]$PackagesPath,
    [string]$WorkspaceDirectory
)

$ErrorActionPreference = 'Stop'
$manifest = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'Sdk.json') -Raw | ConvertFrom-Json
$feed = Join-Path $PSScriptRoot 'Packages'
if (!$UserConfig) { $UserConfig = Join-Path $env:APPDATA 'NuGet\NuGet.Config' }
if (!$WorkspaceDirectory) { $WorkspaceDirectory = Join-Path $PSScriptRoot 'AppCache\SdkRestore' }
$UserConfig = [IO.Path]::GetFullPath($UserConfig)
$WorkspaceDirectory = [IO.Path]::GetFullPath($WorkspaceDirectory)
if (!$PackagesPath) {
    $location = & dotnet nuget locals global-packages --list --force-english-output
    if ($LASTEXITCODE) { throw "Cannot locate NuGet's global-packages directory ($LASTEXITCODE)." }
    $PackagesPath = ([string]($location | Where-Object { $_ -like 'global-packages:*' })).Substring('global-packages:'.Length).Trim()
}
$PackagesPath = [IO.Path]::GetFullPath($PackagesPath)
Write-Host "UsefulNotification offline SDK $($manifest.version)"
Write-Host "Package source: $feed"
Write-Host "User configuration: $UserConfig"
Write-Host "Global packages: $PackagesPath"

$missing = @()
foreach ($package in $manifest.packages) {
    if ($package.id -notmatch '^[A-Za-z0-9_.-]+$' -or $package.version -notmatch '^[A-Za-z0-9_.+-]+$') {
        throw 'Sdk.json contains an invalid package identity.'
    }
    $archive = Join-Path $feed $package.archive
    if (!(Test-Path -LiteralPath $archive -PathType Leaf)) { throw "Offline package is missing: $archive" }
    $hasher = [Security.Cryptography.SHA512]::Create()
    $stream = [IO.File]::OpenRead($archive)
    try { $hash = [Convert]::ToBase64String($hasher.ComputeHash($stream)) }
    finally { $stream.Dispose(); $hasher.Dispose() }
    if ($hash -ne $package.sha512) { throw "Offline package checksum does not match Sdk.json: $archive" }

    $packageDirectory = Join-Path (Join-Path $PackagesPath $package.id.ToLowerInvariant()) $package.version.ToLowerInvariant()
    $complete = (Test-Path -LiteralPath (Join-Path $packageDirectory '.nupkg.metadata') -PathType Leaf) -and
        (Test-Path -LiteralPath (Join-Path $packageDirectory "$($package.id.ToLowerInvariant()).$($package.version.ToLowerInvariant()).nupkg") -PathType Leaf) -and
        (Test-Path -LiteralPath (Join-Path $packageDirectory "$($package.id.ToLowerInvariant()).$($package.version.ToLowerInvariant()).nupkg.sha512") -PathType Leaf)
    if ($complete) {
        $cacheHash = (Get-Content -LiteralPath (Join-Path $packageDirectory "$($package.id.ToLowerInvariant()).$($package.version.ToLowerInvariant()).nupkg.sha512") -Raw).Trim()
        $complete = $cacheHash -eq $package.sha512
    }
    foreach ($file in $package.files) {
        if (!(Test-Path -LiteralPath (Join-Path $packageDirectory $file) -PathType Leaf)) { $complete = $false; break }
    }
    if ($complete) {
        Write-Host "Present: $($package.id) $($package.version)"
    } else {
        Write-Host "Missing or incomplete: $($package.id) $($package.version)"
        $missing += [pscustomobject]@{ Package = $package; Directory = $packageDirectory }
    }
}

if (!(Test-Path -LiteralPath $UserConfig -PathType Leaf)) {
    New-Item -ItemType Directory -Path (Split-Path $UserConfig -Parent) -Force | Out-Null
    '<configuration />' | Set-Content -LiteralPath $UserConfig -Encoding UTF8
}
[xml]$configuration = Get-Content -LiteralPath $UserConfig -Raw
$sourceName = 'UsefulNotification'
$source = @($configuration.SelectNodes('/configuration/packageSources/add')) |
    Where-Object { $_.GetAttribute('key') -eq $sourceName } | Select-Object -First 1
$sourceMatches = $source -and ($source.GetAttribute('value').TrimEnd('\', '/') -eq $feed.TrimEnd('\', '/'))
if (!$source) {
    & dotnet nuget add source $feed --name $sourceName --configfile $UserConfig
} elseif (!$sourceMatches) {
    & dotnet nuget update source $sourceName --source $feed --configfile $UserConfig
}
if ((!$source -or !$sourceMatches) -and $LASTEXITCODE) { throw "NuGet source registration failed ($LASTEXITCODE)." }
$disabled = @($configuration.SelectNodes('/configuration/disabledPackageSources/add')) |
    Where-Object { $_.GetAttribute('key') -eq $sourceName -and $_.GetAttribute('value') -eq 'true' }
if ($disabled) {
    & dotnet nuget enable source $sourceName --configfile $UserConfig
    if ($LASTEXITCODE) { throw "Cannot enable the NuGet source ($LASTEXITCODE)." }
}
Write-Host "Registered source: $sourceName"
if ($missing.Count -eq 0) {
    Write-Host 'All exact package versions are complete; no restore is needed.'
    return
}

New-Item -ItemType Directory -Path $WorkspaceDirectory -Force | Out-Null
foreach ($item in $missing) {
    if (Test-Path -LiteralPath $item.Directory) {
        $backupRoot = Join-Path $WorkspaceDirectory 'IncompletePackages'
        New-Item -ItemType Directory -Path $backupRoot -Force | Out-Null
        $backup = Join-Path $backupRoot "$($item.Package.id).$($item.Package.version).$([Guid]::NewGuid().ToString('N'))"
        if (![IO.Path]::GetFullPath($item.Directory).StartsWith($PackagesPath.TrimEnd('\', '/') + [IO.Path]::DirectorySeparatorChar,
                [StringComparison]::OrdinalIgnoreCase) -or
            ![IO.Path]::GetFullPath($backup).StartsWith($WorkspaceDirectory.TrimEnd('\', '/') + [IO.Path]::DirectorySeparatorChar,
                [StringComparison]::OrdinalIgnoreCase)) {
            throw 'Incomplete package relocation escaped its expected directory.'
        }
        Move-Item -LiteralPath $item.Directory -Destination $backup
        Write-Host "Retained incomplete package at $backup"
    }
}
$project = Join-Path $WorkspaceDirectory 'SdkRestore.csproj'
@"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup><TargetFramework>$($manifest.targetFramework)</TargetFramework><PlatformTarget>x64</PlatformTarget><NuGetAudit>false</NuGetAudit></PropertyGroup>
  <ItemGroup><PackageReference Include="UsefulNotification.Core" Version="[$($manifest.version)]" /></ItemGroup>
</Project>
"@ | Set-Content -LiteralPath $project -Encoding UTF8
$offlineConfig = Join-Path $WorkspaceDirectory 'NuGet.Config'
$escapedFeed = [Security.SecurityElement]::Escape($feed)
@"
<configuration>
  <packageSources><clear /><add key="UsefulNotification" value="$escapedFeed" /></packageSources>
  <fallbackPackageFolders><clear /></fallbackPackageFolders>
  <packageSourceMapping><clear /></packageSourceMapping>
</configuration>
"@ | Set-Content -LiteralPath $offlineConfig -Encoding UTF8
Write-Host "Restoring missing packages from $feed"
& dotnet restore $project --configfile $offlineConfig --packages $PackagesPath --force-evaluate --nologo
if ($LASTEXITCODE) { throw "Offline SDK restore failed ($LASTEXITCODE)." }
Write-Host 'Offline restore completed. New projects can reference UsefulNotification.Core directly.'
