# UsefulNotification

**UsefulNotification just works!**

A Windows notification application built with .NET MAUI Blazor Hybrid. Notifications use the application's own HTML interface and borderless windows. The Windows notification service does not decide whether a notification is displayed.

Designed for programs and scripts, with a tray panel for people. Notification contracts, rendering, themes, and scheduling are included in the source.

![Windows](https://img.shields.io/badge/Windows-x64-3578b9) ![.NET](https://img.shields.io/badge/.NET-10-512bd4) ![License](https://img.shields.io/badge/License-MIT-487f57)

## Use

Extract the Windows x64 archive from [Releases](https://github.com/muhammetozeski/UsefulNotification/releases). Keep the entire folder together. This framework-dependent build requires the x64 .NET 10 runtime, Windows App Runtime 1.7, and the Microsoft Edge WebView2 Runtime.

Run `UsefulNotification.exe` once from its permanent portable folder to open the panel and register its location for the current Windows account. Run it again after moving the folder to refresh that location. The tray icon reopens the panel after closing. The optional Start with Windows setting launches it in the background at sign-in.

From a terminal in the extracted program folder:

```powershell
.\notify.exe
.\notify.exe "Backup complete" "All files were copied." --theme Success
```

The first command needs no arguments. The broker supplies useful content, the sender and timestamp. Select Base, Daily, Success, Attention, Urgent or Reminder to choose colors, sound and delivery together. Programs can also use the C# interface or the local named-pipe protocol.

For a .NET 10 x64 Windows caller, install the matching software development kit (SDK) as a shared local NuGet feed, then add `UsefulNotification.Core` version `3.0.0` to each caller project. The SDK contains the exact dependency packages and IntelliSense documentation. After the host's first launch, callers in other folders can send with the default client options:

```csharp
using UsefulNotification;

await NotificationClient.SendAsync(new()
{
    Title = "Backup complete",
    Theme = NotificationThemes.Success
});
```

The [integration guide](Docs/Integration.md) provides SDK installation and package reference steps, host discovery, partial updates, typed actions, and receiving the result of a specific display.

## Features

- Resident HTML renderer and background request broker; notifications do not take foreground focus.
- Complete inheritable C# themes for colors, sound and delivery, with optional per-send field overrides.
- Text, reminders, progress, images, choices, replies, checklists, and complete custom HTML cards.
- Live updates, priority queues, acknowledgement results, snooze, and recurring schedules.
- Built-in paired light/dark palettes, editable themes and templates, soft synthesized sounds and local audio files.
- Searchable history, tray access, global panel shortcut, and Turkish/English interface resources.
- Portable settings and data, atomic journal commits with backups, and confirmed on-screen delivery.

## Integrate and modify

- [Program and script integration](Docs/Integration.md)
- [HTML and theme customization](Docs/Appearance.md)
- [Build the source](Docs/Build.md)
- [Change the source](Docs/Development.md)
- [Panel interaction and window input](Docs/Interface.md)
- [Documentation index](Docs/Index.md)

The executables are digitally signed. To let Windows verify the signature, run `Install-Certificate.cmd` from `SignatureTrust.zip` once. The programs run without it; only the signature stays unverified.
