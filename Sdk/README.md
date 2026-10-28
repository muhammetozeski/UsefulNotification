# UsefulNotification C# SDK

The SDK contains `UsefulNotification.Core` as a NuGet package, its XML IntelliSense documentation, the MIT license, and the exact resolved dependency packages. It targets .NET 10 on Windows x64. The desktop notification host is distributed separately; a caller does not need MAUI or a reference to the renderer project.

## Install once for your Windows account

Keep this entire SDK folder in a permanent location. On the developer's computer it is `C:\E\kp\aaBenimProgramlarim\NugetPaketleri\UsefulNotification`. In PowerShell 7 or Windows PowerShell 5.1, run:

```powershell
& 'C:\E\kp\aaBenimProgramlarim\NugetPaketleri\UsefulNotification\Install.ps1'
```

The .NET 10 SDK must already be installed. Installation needs no network connection or administrator rights. The script registers its adjacent `Packages` folder as the `UsefulNotification` source in the account's NuGet configuration and fills NuGet's configured global package cache through an ordinary offline restore. Other package sources and package versions are preserved. Keep the SDK folder in place so NuGet can restore again after cache cleanup; if you move it, run its installer again.

Every invocation writes the source, configuration and cache paths, the state of each exact package version, and the actual restore result to the console. Complete packages return immediately without restoring. Missing package files are repaired from the bundled archives. An incomplete cache directory is retained under the SDK's disposable `AppCache` before NuGet restores a complete replacement. The script is noninteractive and exits when finished, so another program can launch it in the background. It creates no startup entry.

## Reference it from any project

Add the package to a Windows .NET 10 x64 project:

```xml
<PackageReference Include="UsefulNotification.Core" Version="2.1.0" />
```

NuGet resolves the DLL, documentation and dependencies for each new project. You do not copy these files into projects. A project that deliberately clears account-level package sources needs to retain the registered local source when its global cache has been cleared.

```csharp
using UsefulNotification;

NotificationReceipt receipt = await NotificationClient.SendAsync(new()
{
    Title = "Backup complete",
    Message = "All files were copied.",
    Theme = NotificationThemes.Happy,
    Sound = NotificationSounds.Subtle
});
```

Launch the desktop host once from its portable folder in your normal Windows session. It records its launcher for this account, so clients can subsequently start it without a path. After moving the host folder, launch it once from the new location to refresh registration. `NotificationClient.Options.Application` remains an optional explicit override. The distribution includes an `Example` console project; run it with `dotnet run --project Example`. See the [integration guide](https://github.com/muhammetozeski/UsefulNotification/blob/main/Docs/Integration.md) for updates, choices, scheduling and results.

## Build the offline distribution

From the source repository, give `Sdk\Build.ps1` an empty output directory in your temporary build workspace:

```powershell
.\Sdk\Build.ps1 -OutputDirectory 'C:\BuildTemp\UsefulNotificationSdk'
```

Building requires PowerShell 7 and uses ordinary NuGet restore with the configured package sources. The script packs Core and restores a real consumer to discover its exact runtime dependency closure. It copies the resolved `.nupkg` archives, installer, license, readme, inventory and example into the output. Private Core build tools are excluded. Intermediates go into the sibling `UsefulNotificationSdk.build` directory unless `-WorkspaceDirectory` selects another location. There is no publication to nuget.org.
