# Sending notifications

UsefulNotification is a resident Windows application with a local notification interface. Run `UsefulNotification.exe` once, or let the adjacent `notify.exe` start it when needed. Closing its panel keeps the broker, scheduler, and HTML renderer running. The tray icon opens the panel; Exit stops the application.

Commands below assume the terminal is in the extracted UsefulNotification folder.

## The smallest command

```powershell
.\notify.exe
```

An empty request receives the calling program's name, a title, a message, the Info level, the current timestamp, and the normal display duration. Notifications use their own windows. The timestamp format is `yyyy.MM.dd HH.mm.ss`.

```powershell
.\notify.exe "Download finished" "The files are ready."
.\notify.exe "Low disk space" "Free some space before continuing." --theme Warn
.\notify.exe "Backup failed" "The destination is unavailable." --theme Error --sticky
.\notify.exe "Backup complete" "All files were copied." --theme Happy --silent
.\notify.exe "Take a break" --type Reminder --after 300
.\notify.exe "Daily review" --type Reminder --at "2026-10-17T09:00:00+03:00" --repeat Daily
```

Numeric durations on the command line mean seconds. Standard .NET durations such as `00:05:00` also work. `--sticky` keeps a card until acknowledgement; `--silent` disables its sound. `--theme` selects a named appearance; `--level` selects meaning independently. `notify.exe --help` lists the complete command syntax.

## JSON input

```powershell
.\notify.exe --json "C:\Notifications\download.json"
'{"title":"Worker","message":"Job completed","silent":true}' | .\notify.exe --stdin
```

The JSON is a `NotificationRequest`. Its properties are defined in [Contracts.cs](../src/UsefulNotification.Core/Contracts.cs). Fields may be omitted. Enum values are case-insensitive when parsed by the command interface; JSON enum values use names such as `info`, `progress`, or `daily`. JSON durations use the .NET TimeSpan representation.

```json
{
  "title": "Download",
  "message": "Receiving files",
  "kind": "progress",
  "tag": "current-download",
  "sticky": true,
  "progress": { "value": 35, "maximum": 100, "label": "35 of 100 files" }
}
```

Each accepted send returns JSON containing `accepted: true` and an `id`. Acceptance means the request was committed to the portable journal. Actual screen delivery is recorded separately in `visibleAt`; it requires rendered content and a native window visible on a monitor.

## Updating and managing notifications

Use the returned id. The following example id is illustrative.

```powershell
.\notify.exe update --id "c2206d17-d20f-4f53-a387-fd841385f0e6" --progress 80
.\notify.exe snooze --id "c2206d17-d20f-4f53-a387-fd841385f0e6" --after 300
.\notify.exe dismiss --id "c2206d17-d20f-4f53-a387-fd841385f0e6"
.\notify.exe list
.\notify.exe panel
.\notify.exe settings
.\notify.exe clear
.\notify.exe exit
```

Updates preserve omitted content. A source/tag pair can replace an existing card. Repeating an identical Send id acknowledges the existing request rather than creating a duplicate. Clear removes completed history and preserves active and future work. The interactive settings command opens the shared console settings menu.

## Calling from C#

Reference `src/UsefulNotification.Core/UsefulNotification.Core.csproj` from a .NET 10 x64 project. This reference does not require the MAUI interface in the calling program.

```csharp
using UsefulNotification;

await NotificationClient.SendAsync();

NotificationResponse receipt = await NotificationClient.SendAsync(new()
{
    Title = "Archive ready",
    Message = "Choose what to do next.",
    Kind = NotificationKind.Choice,
    Theme = "Happy",
    Actions =
    [
        new() { Id = "keep", Label = "Keep" },
        new() { Id = "open", Label = "Open", Kind = NotificationActionKind.OpenUri, Target = "https://example.com/archive" }
    ]
});

if (receipt.Accepted && receipt.Id is Guid id)
{
    NotificationResult result = await NotificationClient.WaitAsync(id);
    Console.WriteLine(result.ActionId);
}
```

The default source is the calling assembly. An external C# caller normally talks to the already-running tray application. To allow it to start a host stored elsewhere, set `AppConstants.Transport.ApplicationPath` to that installation's `AppData\UsefulNotification.exe`. Alternatively, launch UsefulNotification before using the interface.

`SendAsync` also accepts management operations, an existing id, a snooze delay, a manual-order target, and cancellation. `WaitAsync` returns the acknowledgement, selected action, reply text, checked item ids, expiry, or delivery failure. Canceling the wait does not dismiss a notification.

## Content and behavior

| Kind | Content |
| --- | --- |
| Text | Title and message with ordinary expiry. |
| Reminder | Scheduling, recurrence, per-request snooze choices, and acknowledgement. |
| Progress | Determinate or indeterminate progress, a maximum, and a status label; update the same id. |
| Image | A local image path or an HTTP, HTTPS, or data image URI. |
| Choice | Declared action buttons and their returned ids. |
| Input | A single-line or multiline reply with optional required validation. |
| Checklist | Selectable items whose stable ids are returned. |
| Custom | A complete editable HTML template, including its own frame and controls. |

The kind can be inferred from supplied progress, image, input, checklist, actions, or schedule content. Levels are Normal, Info, Success, Warning, Error, Urgent, Deadline, Meeting, Health, Break, and Personal. Priority controls queue order. Custom content allows applications to build additional presentation types without changing the broker.

Scheduling supports one-time, hourly, daily, weekdays, selected weekdays, selected month days, yearly, and explicit intervals. Snoozing changes the next deadline while preserving the recurring series anchor. Sound can use a named editable tone sequence, a request-specific sequence, a local audio file, or silence.

## Results and other languages

The local named pipe is `UsefulNotification.v1`. It accepts a single UTF-8 JSON line **without a byte-order mark**, followed by a newline; flush the writer and read one response line. The server accepts only the current Windows user.

```json
{"operation":"send","notification":{"message":"Job completed","silent":true}}
```

The server fills an omitted source from the connected process. Protocol fields are the `NotificationCommand` and `NotificationResponse` types in the public contract. Operations include Send, Update, Get, List, Dismiss, Snooze, Delete, Move, Clear, ShowPanel, ReloadSettings, and Shutdown.

For a push result, create a named-pipe server in the calling program and set `callbackPipe` to its name. User interaction sends one BOM-free UTF-8 JSON result line to that pipe. The result remains in the journal even when the callback recipient is unavailable. `--wait` is the command-line equivalent for waiting for a result.

## Data

`UserData` contains settings, history, language files, themes, and templates. `AppCache` contains logs and disposable application data. Move the whole program folder to keep its data together. A single writer commits JSON snapshots with backup recovery; a failed commit is rejected and does not leave the uncommitted request in memory.
