# Building and packaging

Use .NET 10 and Visual Studio with the MAUI Windows workload and Windows application packaging tools. Open `UsefulNotification.slnx` for source changes. `UsefulNotification.Core` contains the public models, client, journal, and shared services; `UsefulNotification` contains the MAUI Blazor renderer. The command and launcher projects are small independent entry points.

From the repository root:

```powershell
.\Publish.ps1
```

The script invokes Visual Studio MSBuild with `/restore /t:Publish` in Release configuration. NuGet uses its normal configured sources, reuses cached packages, and restores missing dependencies automatically. Do not replace this with manually downloaded packages or a source restricted to the global cache.

The Visual Studio build host is deliberate: with the .NET 10.0.402 SDK and this Windows App SDK configuration, `dotnet build` reached a packaging task whose `Microsoft.Build.Packaging.Pri.Tasks.dll` was absent from the SDK location. Visual Studio MSBuild completed the same Windows build with its installed packaging tools.

Keep `EnableMsixTooling=true` together with `WindowsPackageType=None`. The former enables the Windows resource publishing targets; the latter keeps the application unpackaged. With tooling disabled, the publish command succeeded but omitted the resource index and the relocated application failed to load MAUI styles. The working output includes `resources.pri`.

The application is published as a framework-dependent, unpackaged Windows x64 folder. Its native GUI and command entry points are SDK-generated apphosts that load their managed entry assemblies from `AppData`. The root GUI entry point relays arguments using `ProcessStartInfo.ArgumentList`; the command entry point loads the command assembly directly. The SDK's `HostWriter.CreateAppHost` performs this layout without single-file bundling.

```text
UsefulNotification/
    UsefulNotification.exe
    notify.exe
    AppData/
    Docs/
    Examples/
```

`UserData` and `AppCache` are created at runtime and are not release content. Package only this clean publish folder. Do not include development journals, logs, credentials, signing keys, or temporary test files.

Before constructing the WinUI WebViews, the host sets the process-local `WEBVIEW2_USER_DATA_FOLDER` to `AppCache\WebView2`. Setting only `BlazorWebViewInitializingEventArgs.UserDataFolder` left this WinUI build using the default folder beside the executable. The relocated release was checked for actual browser files under the cache directory and no browser data inside `AppData`.

Windows executable signing is performed after behavior verification and before local installation or release packaging. The launcher, command host, and application host are signed; third-party libraries are not re-signed. Copying or signing does not require repeating unchanged behavior checks.
