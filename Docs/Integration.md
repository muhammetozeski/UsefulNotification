# Program and script integration

UsefulNotification runs a resident broker, scheduler, and HTML renderer. Closing the panel leaves them running; the tray icon reopens it. Start `UsefulNotification.exe` yourself or allow the command/client to start it in the background.

## Start with an empty notification

From the extracted application folder:

```powershell
.\notify.exe
.\notify.exe "Backup complete" "All files were copied." --theme Happy
.\notify.exe "Backup failed" "The destination is unavailable." --theme Error --sticky
```

The empty command supplies the calling program's name, title, message, Info level, current timestamp, and normal display lifetime. The timestamp format is `yyyy.MM.dd HH.mm.ss`. Normal text expires after eight seconds of confirmed display; interaction, unfinished progress, and scheduled content normally wait for acknowledgement. `--silent` disables sound for one request.

Command durations are seconds or .NET durations such as `00:05:00`. `notify.exe --help` describes the complete syntax in the selected interface language.

## Call from C#

Reference `UsefulNotification.Core.csproj` from a Windows .NET 10 x64 program, or reference the distributed `AppData\UsefulNotification.Core.dll` and its matching dependencies. Keep `UsefulNotification.Core.xml` beside that DLL for IntelliSense descriptions. The caller does not need the MAUI renderer project.

```csharp
using UsefulNotification;

NotificationClient.Options = new NotificationClientOptions
{
    Application = new FileInfo(
        @"C:\Tools\UsefulNotification\UsefulNotification.exe")
};

await NotificationClient.SendAsync();

NotificationReceipt receipt = await NotificationClient.SendAsync(new()
{
    Title = "Archive ready",
    Message = "The files are available.",
    Theme = NotificationThemes.Happy,
    Sound = NotificationSounds.Subtle
});

if (!receipt.Accepted)
    Console.Error.WriteLine($"{receipt.Failure?.Code}: {receipt.Failure?.Message}");
```

Set `Application` to the installed launcher when the host lives elsewhere. The default endpoint is the local `UsefulNotification.v1` pipe; the version 2 client checks the host's `protocolVersion`. An already running host needs no application path. `StartApplication = false` disables automatic startup.

`NotificationClient.Options` owns the defaults. Operations also accept a `NotificationClientOptions` object for call-specific configuration. It contains `FileInfo`, `TimeSpan`, and actual Polly retry/timeout option objects. Send captures the complete request before awaiting a connection: later edits to a theme, sound, action, or collection affect future sends. Avoid mutating an object concurrently with the call that captures it.

Host rejections return `Accepted = false` with a `NotificationFailure.Code`. Serialization, cancellation, startup, and transport failures can throw; handle them at the calling program's operation boundary.

## Receive a user's decision

```csharp
var receipt = await NotificationClient.SendAsync(new()
{
    Title = "Review the report",
    Message = "Choose an action.",
    Theme = NotificationThemes.Info,
    Actions =
    [
        new OpenUriAction(new Uri("https://example.com/report"))
        {
            Label = "Open report"
        },
        new CallbackAction { Id = "later", Label = "Later" }
    ]
});

if (receipt.Accepted)
{
    NotificationResult result = await NotificationClient.WaitAsync(receipt);
    Console.WriteLine($"{result.Status}: {result.ActionId}");
}
```

`WaitAsync(receipt)` targets that exact display occurrence. Dismissal, expiry, snooze, delivery failure, and declared actions produce stored results. The result includes the notification and occurrence identities, selected action, reply, checked item IDs, and any external action outcome. `WaitAsync(id)` retrieves the latest completed result in a series; use the receipt when a later recurrence must not change which result you await. Cancellation stops waiting and leaves the notification alone.

A notification keeps its ID across ordinary updates. Snooze, retry, and the next recurrence create a new occurrence. Completed occurrence history is bounded; a result removed by retention or deletion is no longer available to polling.

Available actions are `DismissAction`, `ReplyAction`, `CallbackAction`, `SnoozeAction(TimeSpan)`, `OpenUriAction(Uri)`, and `RunCommandAction(ProcessStartInfo)`. An omitted action ID receives a unique ID. Supply your own ID when the caller or custom HTML needs a stable business identifier.

`OpenUriAction` accepts absolute HTTP, HTTPS, and mail links. `RunCommandAction` transfers the executable, arguments or `ArgumentList`, working directory, shell/window options, and window style. PATH executable names are supported. Credentials, redirected streams, shell verbs, and custom environment overrides are rejected. A successful command outcome means Windows accepted the start operation; it does not wait for the launched program to finish.

For an external action, the host persists acceptance before starting it, then persists its outcome before publishing the result. It executes the action outside the journal lock. Completed or stale controls cannot repeat it. An action interrupted before its outcome was recorded is marked interrupted after restart and is not replayed automatically.

## Update only the fields you supply

```csharp
var receipt = await NotificationClient.SendAsync(new()
{
    Title = "Download",
    Progress = new() { Value = 0, Maximum = 1000, Label = "Receiving files" }
});

if (receipt.Accepted)
{
    await NotificationClient.UpdateAsync(receipt.NotificationId, new()
    {
        Progress = new() { Value = new(80) },
        Actions = new([]),
        Schedule = new(null)
    });
}
```

This changes progress to 80 while retaining its maximum and label, clears the action list, and removes scheduling. Every omitted field stays unchanged, including the sender. `Change<T>` records that a field was supplied; its value expresses the replacement. A supplied null clears a nullable field. Empty collections clear their contents.

`Progress` patches individual progress fields; `ProgressContent = new(null)` removes the whole progress content. On a visible card, supplying Lifetime restarts an expiring duration from the update, or removes expiry for acknowledgement. Clearing Lifetime restores the content-based default. A change in that effective default, such as finishing progress, also updates expiry. An existing viewport/hover pause is retained.

`Replace` edits a complete request. It keeps an active occurrence when its schedule is unchanged, and creates a new occurrence when rescheduled or already completed. `Retry` immediately creates a new occurrence with the same request and series anchor, including when retrying a future reminder. Use `UpdateAsync` for ordinary live changes.

Management uses the same typed endpoint:

```csharp
await NotificationClient.ExecuteAsync(new()
{
    Operation = NotificationOperation.Snooze,
    Id = receipt.NotificationId,
    OccurrenceId = receipt.OccurrenceId,
    Delay = TimeSpan.FromMinutes(5)
});
```

Supplying `OccurrenceId` guards an update, dismissal, or snooze against acting on a newer display. Commands also include Get, List, Retry, Delete, Move, Clear, ShowPanel, ReloadSettings, and Shutdown. Clear removes completed series while retaining active and future work. Repeating a Send ID acknowledges the existing send; a source/tag pair can update the corresponding card.

```powershell
.\notify.exe update --id "c2206d17-d20f-4f53-a387-fd841385f0e6" --progress 80
.\notify.exe get --id "c2206d17-d20f-4f53-a387-fd841385f0e6"
.\notify.exe snooze --id "c2206d17-d20f-4f53-a387-fd841385f0e6" --after 300
.\notify.exe dismiss --id "c2206d17-d20f-4f53-a387-fd841385f0e6"
.\notify.exe list
.\notify.exe panel
.\notify.exe clear
.\notify.exe exit
```

## Content, lifetime, and scheduling

Content is selected by the objects supplied. A notification can combine progress, image, input, checklist, actions, and scheduling. `Template` replaces the entire card with HTML; see [HTML and themes](Appearance.md).

| Need | Object to supply |
| --- | --- |
| Plain text | `Title` and `Message`, or an empty request |
| Progress | `ProgressContent`; null `Value` means indeterminate, default `Maximum` is 100 |
| Image | `NotificationImage(new FileInfo(path))` or `NotificationImage(new Uri(url))` |
| Written reply | `InputContent` and optionally a declared `ReplyAction` |
| Checklist | `ChecklistItem` objects with stable IDs; selected IDs return in the result |
| Custom card | `HtmlTemplateContent(new FileInfo(path))`; omitted file uses the editable default |
| Fixed display lifetime | `ExpiringLifetime(TimeSpan)` |
| Wait for acknowledgement | `NotificationLifetimes.UntilAcknowledged` |
| One-time reminder | `ScheduleOptions.At` or `ScheduleOptions.After` |
| Repeated reminder | `ScheduleOptions.Recurrence` with a recurrence object |
| Custom sound | `SoundPattern` with tone steps or an `AudioFile` |

Images accept local files and absolute HTTP, HTTPS, or data image URIs. For a scheduled reminder:

```csharp
await NotificationClient.SendAsync(new()
{
    Title = "Review pending jobs",
    Schedule = new()
    {
        After = TimeSpan.FromMinutes(5),
        Recurrence = NotificationRecurrences.Daily
    },
    Snooze = [TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(15)]
});
```

Use either `At` or `After`. `IntervalRecurrence`, `DailyRecurrence`, `WeeklyRecurrence`, `MonthlyRecurrence`, and `YearlyRecurrence` carry their own calculation. Ready-to-use Hourly, Daily, Weekdays, Weekly, Monthly, and Yearly objects are in `NotificationRecurrences`. Empty weekly/monthly day lists retain the original anchor day; the anchored monthly policy clamps short months. Explicit monthly day lists skip unavailable dates. Calendar calculations retain the anchor's offset. Snooze preserves the series anchor.

## JSON and other languages

The command accepts a request object from a file or standard input:

```powershell
.\notify.exe --json "C:\Notifications\download.json"
'{"title":"Worker","message":"Job completed","silent":true}' | .\notify.exe --stdin
```

Version 2 object shapes:

```json
{
  "title": "Download",
  "tag": "current-download",
  "lifetime": { "mode": "acknowledge" },
  "progress": { "value": 35, "maximum": 1000, "label": "Receiving files" },
  "schedule": {
    "after": "00:05:00",
    "recurrence": { "pattern": "interval", "every": "01:00:00" }
  }
}
```

The text boundary also accepts version 1 inputs: named themes, `kind`, `sticky`/`duration`, image/template path strings, named recurrence plus its old options, and legacy action fields. It converts them at the host. C# callers pass actual objects; the object serializer does not resolve theme names or scan files. A theme name in CLI/JSON is a text-interface convenience. The version 2 client requires a version 2 host; update both when moving from the earlier C# interface.

For direct communication, open the `UsefulNotification.v1` named pipe on the same Windows computer. Send one UTF-8 JSON line without a byte-order mark, add a newline, flush, and read one response line. The host restricts connections to the current Windows user and limits a line to 1,048,576 characters.

```json
{"operation":"send","notification":{"title":"Job completed","silent":true}}
```

An accepted response contains `accepted`, `id`, `occurrenceId`, and `protocolVersion: 2`. Acceptance follows journal persistence; screen delivery is recorded separately in the occurrence's `visibleAt`. A rejection contains `failure.code` and `failure.message`.

Partial updates use field presence directly:

```json
{"operation":"update","id":"c2206d17-d20f-4f53-a387-fd841385f0e6","update":{"progress":{"value":80},"actions":[],"schedule":null}}
```

Use `notify.exe update --id <id> --json <file>` or `--stdin` for a patch. An omitted field is retained; `null` explicitly clears a nullable field. `--actions ""` and `--items ""` empty their lists. `--progress` updates only Value.

For pipe polling, send Get and inspect the matching occurrence's `result` in `entries[].occurrences`. For a push result, the caller first creates its own named-pipe server and sets `callbackPipe` in the request. The host sends the same stored result as one BOM-free UTF-8 JSON line. Callback delivery is best effort; the journal retains the result if the recipient is unavailable. `notify.exe --wait` waits for the accepted occurrence.

## Data and source definitions

`UserData` contains settings, notification history, language files, themes, and templates. `AppCache` contains logs and disposable renderer data. Move the whole program folder to retain its data. Failed journal commits reject the operation and restore the last committed in-memory state.

The field reference is the library's IntelliSense XML plus [contracts](https://github.com/muhammetozeski/UsefulNotification/blob/main/src/UsefulNotification.Core/Contracts.cs), [client](https://github.com/muhammetozeski/UsefulNotification/blob/main/src/UsefulNotification.Core/NotificationClient.cs), [actions](https://github.com/muhammetozeski/UsefulNotification/blob/main/src/UsefulNotification.Core/Actions.cs), and [recurrences](https://github.com/muhammetozeski/UsefulNotification/blob/main/src/UsefulNotification.Core/Recurrences.cs). To change the application itself, continue with [the development guide](Development.md).
