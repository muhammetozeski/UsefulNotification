# UsefulNotification

**UsefulNotification just works!**

A Windows notification application built with .NET MAUI Blazor Hybrid. Notifications use the application's own HTML interface and borderless windows. The Windows notification service does not decide whether a notification is displayed.

Designed for programs and scripts, with a tray panel for people. Notification contracts, rendering, themes, and scheduling are included in the source.

![Windows](https://img.shields.io/badge/Windows-x64-3578b9) ![.NET](https://img.shields.io/badge/.NET-10-512bd4) ![License](https://img.shields.io/badge/License-MIT-487f57)

## Use

Extract the Windows x64 archive from [Releases](https://github.com/muhammetozeski/UsefulNotification/releases). Keep the entire folder together. This framework-dependent build requires the x64 .NET 10 runtime, Windows App Runtime 1.7, and the Microsoft Edge WebView2 Runtime.

Run `UsefulNotification.exe` to open the panel. The tray icon reopens it after closing. The optional Start with Windows setting launches it in the background at sign-in.

From a terminal in the extracted program folder:

```powershell
.\notify.exe
.\notify.exe "Backup complete" "All files were copied." --theme Happy
```

The first command needs no arguments. The broker supplies useful content, the sender, level, and timestamp. Programs can also use the C# interface or the local named-pipe protocol.

For a .NET 10 x64 Windows caller, reference the Core project or the distributed Core library and matching dependencies:

```csharp
using UsefulNotification;

await NotificationClient.SendAsync(new()
{
    Title = "Backup complete",
    Theme = NotificationThemes.Happy,
    Sound = NotificationSounds.Subtle
});
```

The [integration guide](Docs/Integration.md) explains host startup, partial updates, typed actions, and receiving the result of a specific display.

## Features

- Resident HTML renderer and background request broker; notifications do not take foreground focus.
- Compact cards with an HTML frame controlled by named themes or per-request C# objects.
- Text, reminders, progress, images, choices, replies, checklists, and complete custom HTML cards.
- Live updates, priority queues, acknowledgement results, snooze, and recurring schedules.
- Built-in palettes, editable themes and templates, customizable tone sequences, and local audio files.
- Searchable history, tray access, global panel shortcut, and Turkish/English interface resources.
- Portable settings and data, atomic journal commits with backups, and confirmed on-screen delivery.

## Integrate and modify

- [Program and script integration](Docs/Integration.md)
- [HTML and theme customization](Docs/Appearance.md)
- [Build the source](Docs/Build.md)
- [Change the source](Docs/Development.md)
- [Documentation index](Docs/Index.md)

The executables are digitally signed. To let Windows verify the signature, run `Install-Certificate.cmd` from `SignatureTrust.zip` once. The programs run without it; only the signature stays unverified.
