[CmdletBinding()]
param(
    [string]$PackageRoot = (Join-Path (Split-Path $PSScriptRoot) 'publish\UsefulNotification'),
    [string]$ExampleSource,
    [string]$OutputDirectory,
    [string]$PayloadDirectory,
    [switch]$ValidateExamples,
    [string]$ValidationDirectory
)
$ErrorActionPreference = 'Stop'
$sdkVersion = (& dotnet --version).Trim()
$sdkLine = & dotnet --list-sdks | Where-Object { $_.StartsWith("$sdkVersion ") } | Select-Object -First 1
$sdkRoot = [regex]::Match($sdkLine, '\[(.+)\]').Groups[1].Value
$sdk = Join-Path $sdkRoot $sdkVersion
$compiler = Join-Path $sdk 'Roslyn\bincore\csc.dll'
$dotnetRoot = Split-Path $sdkRoot
$references = @('Microsoft.NETCore.App.Ref','Microsoft.WindowsDesktop.App.Ref') | ForEach-Object {
    $pack = Get-ChildItem -LiteralPath (Join-Path $dotnetRoot "packs\$_") -Directory | Where-Object { $_.Name.StartsWith('10.') } | Sort-Object { [version]$_.Name } -Descending | Select-Object -First 1
    if (-not $pack) { throw "The installed .NET 10 reference pack is required: $_" }
    Get-ChildItem -LiteralPath (Join-Path $pack.FullName 'ref\net10.0') -Filter '*.dll' -File
}

function Compile-CSharp([string]$Source, [string]$Output, [bool]$Gui) {
    # All compiler inputs are explicit; no project build, package restore or manual downloads are involved.
    [IO.Directory]::CreateDirectory((Split-Path $Output)) | Out-Null
    $arguments = [Collections.Generic.List[string]]::new()
    $arguments.AddRange([string[]]@('/nostdlib+','/langversion:14.0','/nullable:enable','/platform:x64','/optimize+','/warnaserror+','/utf8output',('/target:' + $(if ($Gui) { 'winexe' } else { 'exe' })),('/out:"' + $Output + '"')))
    foreach ($reference in $references) { $arguments.Add('/reference:"' + $reference.FullName + '"') }
    foreach ($name in @('UsefulNotification.Core.dll','Polly.Core.dll')) { $arguments.Add('/reference:"' + (Join-Path $PayloadDirectory $name) + '"') }
    if ($Gui) { $icon = Join-Path (Split-Path $PSScriptRoot) 'Assets\app.ico'; if (-not (Test-Path -LiteralPath $icon)) { $icon = Join-Path $PayloadDirectory 'app.ico' }; $arguments.Add('/win32icon:"' + $icon + '"') }
    $arguments.Add('"' + $Source + '"')
    $responseFile = $Output + '.rsp'
    [IO.File]::WriteAllLines($responseFile, $arguments, [Text.UTF8Encoding]::new($false))
    try { & dotnet $compiler /noconfig ('@' + $responseFile); if ($LASTEXITCODE -ne 0) { throw "C# compilation failed: $Source" } }
    finally { if (Test-Path -LiteralPath $responseFile) { Remove-Item -LiteralPath $responseFile } }
    $runtime = @{ runtimeOptions = @{ tfm = 'net10.0'; framework = @{ name = 'Microsoft.WindowsDesktop.App'; version = '10.0.0' } } }
    [IO.File]::WriteAllText([IO.Path]::ChangeExtension($Output, '.runtimeconfig.json'), ($runtime | ConvertTo-Json -Depth 5))
}

if (-not $PayloadDirectory) { $PayloadDirectory = Join-Path $PackageRoot 'AppData' }
if ($ExampleSource) {
    if (-not $OutputDirectory) { throw 'OutputDirectory is required for an example.' }
    foreach ($name in @('UsefulNotification.Core.dll','Polly.Core.dll')) { Copy-Item -LiteralPath (Join-Path $PayloadDirectory $name) -Destination $OutputDirectory -Force }
    Compile-CSharp $ExampleSource (Join-Path $OutputDirectory 'UsefulNotification.Demo.Example.dll') $false
    exit
}
if ($ValidateExamples) {
    if (-not $ValidationDirectory) { throw 'ValidationDirectory must be a disposable directory.' }
    $template = [IO.File]::ReadAllText((Join-Path $PSScriptRoot 'Runner.cs.template'))
    $catalog = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'Catalog.json') -Raw | ConvertFrom-Json
    foreach ($example in $catalog) {
        $directory = Join-Path $ValidationDirectory $example.Id
        [IO.Directory]::CreateDirectory($directory) | Out-Null
        $source = Join-Path $directory 'Example.cs'
        [IO.File]::WriteAllText($source, $template.Replace('{{CODE}}', [IO.File]::ReadAllText((Join-Path $PSScriptRoot ('Examples\' + $example.File)))))
        Compile-CSharp $source (Join-Path $directory 'UsefulNotification.Demo.Example.dll') $false
        Write-Output ('Compiled: ' + $example.Id)
    }
    Write-Output ('Compiled examples: ' + $catalog.Count)
    exit
}
Compile-CSharp (Join-Path $PSScriptRoot 'Program.cs') (Join-Path $PayloadDirectory 'UsefulNotification.Demo.dll') $true
Add-Type -LiteralPath (Join-Path $sdk 'Microsoft.NET.HostModel.dll')
$pack = Get-ChildItem -LiteralPath (Join-Path $dotnetRoot 'packs\Microsoft.NETCore.App.Host.win-x64') -Directory | Where-Object Name -Like '10.*' | Sort-Object { [version]$_.Name } -Descending | Select-Object -First 1
$appHost = Join-Path $pack.FullName 'runtimes\win-x64\native\apphost.exe'
[Microsoft.NET.HostModel.AppHost.HostWriter]::CreateAppHost($appHost, (Join-Path $PackageRoot 'UsefulNotification.Demo.exe'), 'AppData\UsefulNotification.Demo.dll', $true, (Join-Path $PayloadDirectory 'UsefulNotification.Demo.dll'), $false, $false, $null)
$demoRoot = Join-Path $PackageRoot 'Demo'
[IO.Directory]::CreateDirectory($demoRoot) | Out-Null
foreach ($name in @('Catalog.json','Runner.cs.template','Build.ps1','Program.cs','README.md')) { Copy-Item -LiteralPath (Join-Path $PSScriptRoot $name) -Destination $demoRoot -Force }
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Examples'),(Join-Path $PSScriptRoot 'Assets') -Destination $demoRoot -Recurse -Force
Write-Output ('Built demo: ' + (Join-Path $PackageRoot 'UsefulNotification.Demo.exe'))
