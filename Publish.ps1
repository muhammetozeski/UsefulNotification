[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$projectRoot = $PSScriptRoot
$publishRoot = [IO.Path]::GetFullPath((Join-Path $projectRoot 'publish'))
$destinationRoot = Join-Path $publishRoot 'UsefulNotification'
$packageRoot = Join-Path $publishRoot 'UsefulNotification.pending'
if (Test-Path -LiteralPath $packageRoot) {
    if ((Resolve-Path -LiteralPath $packageRoot).Path -ne [IO.Path]::GetFullPath($packageRoot)) { throw 'Unexpected staging path.' }
    [Microsoft.VisualBasic.FileIO.FileSystem]::DeleteDirectory($packageRoot, 'OnlyErrorDialogs', 'SendToRecycleBin')
}
$payloadRoot = Join-Path $packageRoot 'AppData'
$locator = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
$buildTool = @(& $locator -latest -products '*' -find 'MSBuild\Current\Bin\amd64\MSBuild.exe')[0]
if (-not $buildTool) { throw 'Visual Studio MSBuild with Windows application packaging tools is required.' }
[IO.Directory]::CreateDirectory($payloadRoot) | Out-Null
foreach ($name in @('UsefulNotification', 'UsefulNotification.Command', 'UsefulNotification.Launcher')) {
    $project = Join-Path $projectRoot "src\$name\$name.csproj"
    & $buildTool $project /restore /t:Publish /p:Configuration=Release "/p:PublishDir=$payloadRoot\" /p:SelfContained=false /verbosity:minimal /nologo
    if ($LASTEXITCODE -ne 0) { throw "Publish failed: $name" }
}
if (-not (Test-Path -LiteralPath (Join-Path $payloadRoot 'resources.pri'))) {
    throw 'The Windows resource index is missing from the publish output. Keep EnableMsixTooling enabled.'
}

# The SDK apphost loads the C# launcher from AppData; no single-file linker or runtime bundle is required.
$sdkVersion = (& dotnet --version).Trim()
$sdkLine = & dotnet --list-sdks | Where-Object { $_.StartsWith("$sdkVersion ") } | Select-Object -First 1
$sdkBase = [regex]::Match($sdkLine, '\[(.+)\]').Groups[1].Value
$hostModel = Join-Path (Join-Path $sdkBase $sdkVersion) 'Microsoft.NET.HostModel.dll'
Add-Type -LiteralPath $hostModel
$dotnetRoot = Split-Path $sdkBase
$hostPack = Get-ChildItem -LiteralPath (Join-Path $dotnetRoot 'packs\Microsoft.NETCore.App.Host.win-x64') -Directory | Sort-Object { [version]$_.Name } -Descending | Select-Object -First 1
$appHost = Join-Path $hostPack.FullName 'runtimes\win-x64\native\apphost.exe'
[Microsoft.NET.HostModel.AppHost.HostWriter]::CreateAppHost(
    $appHost, (Join-Path $packageRoot 'UsefulNotification.exe'),
    'AppData\UsefulNotification.Launcher.dll', $true,
    (Join-Path $payloadRoot 'UsefulNotification.Launcher.dll'), $false, $false, $null)
[Microsoft.NET.HostModel.AppHost.HostWriter]::CreateAppHost(
    $appHost, (Join-Path $packageRoot 'notify.exe'),
    'AppData\UsefulNotification.Command.dll', $false,
    (Join-Path $payloadRoot 'UsefulNotification.Command.dll'), $false, $false, $null)
Copy-Item -LiteralPath (Join-Path $projectRoot 'README.md') -Destination $packageRoot -Force
Copy-Item -LiteralPath (Join-Path $projectRoot 'LICENSE') -Destination $packageRoot -Force
Copy-Item -LiteralPath (Join-Path $projectRoot 'Docs') -Destination $packageRoot -Recurse -Force
Copy-Item -LiteralPath (Join-Path $projectRoot 'Examples') -Destination $packageRoot -Recurse -Force
if (Test-Path -LiteralPath $destinationRoot) {
    if ((Resolve-Path -LiteralPath $destinationRoot).Path -ne [IO.Path]::GetFullPath($destinationRoot)) { throw 'Unexpected publish path.' }
    [Microsoft.VisualBasic.FileIO.FileSystem]::DeleteDirectory($destinationRoot, 'OnlyErrorDialogs', 'SendToRecycleBin')
}
[IO.Directory]::Move($packageRoot, $destinationRoot)
Write-Output "Published: $destinationRoot"
