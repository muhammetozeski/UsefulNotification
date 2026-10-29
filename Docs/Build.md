# Building and packaging

Use .NET 10 and Visual Studio with the MAUI Windows workload and Windows application packaging tools. Open `UsefulNotification.slnx` for source changes. `UsefulNotification.Core` contains the public models, client, journal, and shared services; `UsefulNotification` contains the MAUI Blazor renderer. The command and launcher projects are small independent entry points.

Create a disposable output folder outside the repository with `NewTemp UsefulNotificationPublish`. From the repository root, pass the full path it returns. For example:

```powershell
.\Publish.ps1 -OutputDirectory "C:\C\UsefulNotificationPublish"
```

The script invokes Visual Studio MSBuild with `/restore /t:Publish` in Release configuration. NuGet uses its normal configured sources, reuses cached packages, and restores missing dependencies automatically. Do not replace this with manually downloaded packages or a source restricted to the global cache.

`-OutputDirectory` is required and must be outside the source tree. Publishing starts in its clean `UsefulNotification.pending` subfolder and replaces its `UsefulNotification` subfolder only after all three entry projects and the Windows resource index are present. Previous staging/output folders go to the Recycle Bin. This prevents stale binaries or runtime data from entering a subsequent package. Keep publishing copies, build workspaces, and release ZIP files under the disposable output root; copy only the final application and SDK to their permanent installation folders. The public Core XML documentation is generated beside `UsefulNotification.Core.dll` and included in `AppData`; the SDK package also carries it for IntelliSense. The GUI launcher uses the same Core error boundary and portable log location as the broker.

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

## Build the caller SDK

`Publish.ps1` also invokes `Sdk\Build.ps1`, creating `UsefulNotification-Sdk` beside the application output and using `SdkBuild` for its disposable build workspace. The SDK can be built separately from the repository root:

```powershell
.\Sdk\Build.ps1 -OutputDirectory "C:\C\UsefulNotificationPublish\UsefulNotification-Sdk" -WorkspaceDirectory "C:\C\UsefulNotificationPublish\SdkBuild"
```

Choose an empty SDK output directory. The builder packages `UsefulNotification.Core` version `3.0.0` with its generated XML documentation, then restores an isolated consumer through the normal configured sources to collect the exact dependency closure. The resulting `Packages` folder contains `UsefulNotification.Core.3.0.0.nupkg` and `polly.core.8.7.0.nupkg`. Beside it are `Install.ps1`, `Sdk.json`, `README.md`, `LICENSE`, and a minimal `Example` caller.

The SDK installer registers a single user-level feed and restores its exact packages offline into the configured global cache. Consumer projects use `PackageReference`; they do not need the source repository or a project-specific feed. See [C# integration](Integration.md#call-from-c) for installation and calling. The main application publish retains normal NuGet restore; the SDK installer's explicit offline restore serves the bundled packages to callers.
