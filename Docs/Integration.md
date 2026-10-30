# Program and script integration

UsefulNotification runs a resident broker, scheduler, and HTML renderer. Closing the panel leaves them running; the tray icon reopens it. Start `UsefulNotification.exe` yourself or allow the command/client to start it in the background.

## Start with an empty notification

From the extracted application folder:

```powershell
.\notify.exe
.\notify.exe "Backup complete" "All files were copied." --Theme Success
.\notify.exe "Backup failed" "The destination is unavailable." --Theme Urgent --Sticky
```

The empty command supplies the calling program's name, title, message and current timestamp. The timestamp format is `yyyy.MM.dd HH.mm.ss`. The selected theme supplies colors, sound and delivery together. `--Silent` writes an explicit null sound override for one request; `--BodyClick` enables card-background acknowledgement while retaining Snooze. Input controls and action buttons keep their own interactions.

Command durations are seconds or .NET durations such as `00:05:00`. `notify.exe --Help` describes the complete syntax in the selected interface language.

## Notify a Codex conversation

Use a version 3.1.0 or later desktop host and command sender. Add the conversation UUID and provider to either equivalent option:

```powershell
.\notify.exe "Question" "Please review the result." --NotifyLLM "c2206d17-d20f-4f53-a387-fd841385f0e6" Codex
.\notify.exe "Question" "Please review the result." -l "c2206d17-d20f-4f53-a387-fd841385f0e6" Codex
```

Replace the UUID with the existing destination conversation. The sender prints acceptance and exits normally. The resident host sends the completed occurrence's JSON result to that conversation through Codex's native `queue --thread <ThreadId> --message <Text>` command. It discovers the newest installed Codex executable under the current account's local `OpenAI\Codex\bin` directory, with PATH as the fallback. It launches the executable directly with separate arguments; no PowerShell helper, terminal window or Codex polling loop is involved.

Delivery follows the completion result: body click, close control, selected action, submitted reply, checked items, snooze, expiry or delivery failure. The message includes `id`, `occurrenceId`, `status`, `at` and any action/reply/checklist result. Every recurring or snoozed display retains its own result identity. Ordinary notifications omit `NotifyLLM` and produce no conversation message.

`--Wait` independently keeps the sender open until its accepted display completes. `--Sticky` independently keeps the card until interaction. `--BodyClick` independently lets the card background acknowledge the notification and retains Snooze. None is required for `--NotifyLLM`; they can be combined as needed. Long option names use PascalCase and are accepted case-insensitively; `-l` is the short alias. The old `--acknowledge-on-click` spelling remains accepted for existing scripts, while help and examples use `--BodyClick`.

C# callers pass the actual destination object:

```csharp
await NotificationClient.SendAsync(new()
{
    Title = "Review complete",
    NotifyLLM = new CodexThreadTarget(conversationId)
});
```

`conversationId` is the destination conversation's `Guid`. JSON callers supply `"notifyLLM": { "threadId": "c2206d17-d20f-4f53-a387-fd841385f0e6" }`. Partial updates retain an omitted destination; `"notifyLLM": null` disables it for later completions. CLI destination options are applied after JSON loading, so `--Json` or `--Stdin` cannot accidentally discard the supplied conversation.

The host journals completion before starting delivery. Each occurrence's `llmDelivery` records the captured destination, `attemptedAt`, confirmed `queuedAt`, or a technical `error`. Queue acceptance means Codex accepted the message, rather than a claim that the model has already answered. Not-yet-started deliveries are recovered at host startup. A started attempt is not automatically repeated after a process error or uncertain shutdown, avoiding duplicate conversation prompts. Its original notification result remains in history. Existing named-pipe callbacks continue independently.

## Call from C#

Use the version `3.1.0` software development kit (SDK) with a Windows .NET 10 x64 caller. Keep the SDK in a permanent folder, separate from the application and caller projects. This installation uses `C:\E\kp\aaBenimProgramlarim\NugetPaketleri\UsefulNotification`; `Install.ps1` must remain beside its `Packages` folder.

Run the installer once for the Windows account:

```powershell
pwsh -NoProfile -File "C:\E\kp\aaBenimProgramlarim\NugetPaketleri\UsefulNotification\Install.ps1"
```

It registers or repairs the `UsefulNotification` package source in the user's NuGet configuration. The source points to the adjacent `Packages` folder, shared by all that user's projects. The installer discovers NuGet's actual configured global package cache, checks the exact package versions and extracted contents, and restores incomplete or missing packages offline from the SDK. Complete cached packages skip restoration. It always runs without questions and writes detailed progress to the console; it does not configure application startup.

The SDK carries `UsefulNotification.Core.3.1.0.nupkg` and its exact dependency `polly.core.8.7.0.nupkg`. The Core package includes its generated XML descriptions for IntelliSense. Each caller needs only a package reference; normal project restore finds the registered user-level feed:

```xml
<ItemGroup>
  <PackageReference Include="UsefulNotification.Core" Version="3.1.0" />
</ItemGroup>
```

Run `UsefulNotification.exe` once from the application's portable folder in your normal Windows session before using automatic startup from other folders. The caller then sends with the default options:

```csharp
using UsefulNotification;

await NotificationClient.SendAsync();

NotificationReceipt receipt = await NotificationClient.SendAsync(new()
{
    Title = "Archive ready",
    Message = "The files are available.",
    Theme = NotificationThemes.Success
});

if (!receipt.Accepted)
    Console.Error.WriteLine($"{receipt.Failure?.Code}: {receipt.Failure?.Message}");
```

All callers use the same local `UsefulNotification.v1` pipe, independently of their working directory. The version 3 client checks the host's `protocolVersion`. When the host is already running, the client connects directly. If it is absent and automatic startup is enabled, a null `Application` first checks the launcher recorded in `HKEY_CURRENT_USER\Software\UsefulNotification`, value `Executable`, then looks beside the client payload. Successful host initialization refreshes the recorded path; launch the host again after moving its portable folder.

`Application` remains an optional override for a specific launcher:

```csharp
NotificationClient.Options = new()
{
    Application = new FileInfo(@"C:\Tools\UsefulNotification\UsefulNotification.exe")
};
```

`StartApplication = false` disables automatic startup. Installing the SDK makes the library available to callers; the application's portable folder supplies the running host and its data.

`NotificationClient.Options` owns the defaults. Operations also accept a `NotificationClientOptions` object for call-specific configuration. It contains `FileInfo`, `TimeSpan`, and actual Polly retry/timeout option objects. Send captures the complete request before awaiting a connection: later edits to a theme, sound, action, or collection affect future sends. Avoid mutating an object concurrently with the call that captures it.

Host rejections return `Accepted = false` with a `NotificationFailure.Code`. Serialization, cancellation, startup, and transport failures can throw; handle them at the calling program's operation boundary.

## Select one complete theme

`NotificationThemes.Base`, `Daily`, `Success`, `Attention`, `Urgent` and `Reminder` are actual theme objects. A theme owns colors, typography, frame, sound, lifetime, queue priority and acknowledgement behavior. Selecting a theme uses those values together. Optional request overrides replace only the fields you supply; omitted fields remain inherited.

```csharp
using System.Drawing;
using UsefulNotification;

var buildTheme = new NotificationTheme
{
    Id = "build-review",
    Name = "Build review",
    Parent = NotificationThemes.Success,
    Overrides = new()
    {
        Palette = new() { Accent = new(Color.CornflowerBlue) },
        Lifetime = new(NotificationLifetimes.UntilAcknowledged)
    }
};

await NotificationClient.SendAsync(new()
{
    Title = "Build complete",
    Theme = buildTheme,
    Interaction = NotificationInteractions.ClickToAcknowledge,
    Overrides = new() { Sound = new(null) }
});
```

`new(null)` explicitly mutes this send. Omitting `Overrides.Sound` retains the inherited sound. `NotificationInteractions.ClickToAcknowledge` enables card-background acknowledgement and retains Snooze; `NotificationInteractions.Actions` uses explicit buttons. Links, inputs, checklist controls and Snooze consume their own interaction before a card-background acknowledgement.

`ThemeResolver.Resolve(request)` returns detached effective values, including `Theme`, `Sound`, `Lifetime`, `Priority`, `Interaction` and `FieldOrigins`. Accepted entries carry their resolved snapshot, so later preset or catalog edits do not alter an already accepted notification. The appearance guide describes parent themes and file-backed definitions.

```powershell
.\notify.exe "Build complete" "Review the output." --Theme Success
.\notify.exe "Read this" "Click the card or postpone it." --Theme Reminder --BodyClick --Silent
```
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
.\notify.exe Update --Id "c2206d17-d20f-4f53-a387-fd841385f0e6" --Progress 80
.\notify.exe Get --Id "c2206d17-d20f-4f53-a387-fd841385f0e6"
.\notify.exe Snooze --Id "c2206d17-d20f-4f53-a387-fd841385f0e6" --After 300
.\notify.exe Dismiss --Id "c2206d17-d20f-4f53-a387-fd841385f0e6"
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
.\notify.exe --Json "C:\Notifications\download.json"
'{"title":"Worker","message":"Job completed","silent":true}' | .\notify.exe --Stdin
```

Version 3 object shapes:

```json
{
  "title": "Download",
  "tag": "current-download",
  "theme": "Reminder",
  "interaction": { "clickToAcknowledge": true, "showSnooze": true },
  "overrides": { "sound": null },
  "progress": { "value": 35, "maximum": 1000, "label": "Receiving files" },
  "schedule": {
    "after": "00:05:00",
    "recurrence": { "pattern": "interval", "every": "01:00:00" }
  }
}
```

The text boundary also accepts version 1 inputs: named themes, `kind`, `sticky`/`duration`, image/template path strings, named recurrence plus its old options, and legacy action fields. It converts them at the host. C# callers pass actual objects; the object serializer does not resolve theme names or scan files. A theme name in CLI/JSON is a text-interface convenience. The version 3 client requires a version 3 host; update both when moving from the earlier C# interface.

For direct communication, open the `UsefulNotification.v1` named pipe on the same Windows computer. Send one UTF-8 JSON line without a byte-order mark, add a newline, flush, and read one response line. The host's pipe permissions grant its Windows account access and deny network access. The C# client checks that the pipe owner matches its own Windows account and connects with identification permission, which lets the host identify the caller. The host limits inbound request lines to 1,048,576 characters. `NotificationClientOptions.MaximumResponseCharacters` separately allows 33,554,432 response characters by default; a retained history can be larger than one request.

```json
{"operation":"send","notification":{"title":"Job completed","theme":"Success","overrides":{"sound":null}}}
```

A version-three command supplies `protocolVersion: 3`; the current SDK and command sender do this automatically. The host responds with version three for those callers and version two for older callers that omit the field or request two. An accepted response contains `accepted`, `id`, and `occurrenceId`. Acceptance follows journal persistence; screen delivery is recorded separately in the occurrence's `visibleAt`. A rejection contains `failure.code` and `failure.message`.

Partial updates use field presence directly:

```json
{"operation":"update","id":"c2206d17-d20f-4f53-a387-fd841385f0e6","update":{"progress":{"value":80},"actions":[],"schedule":null}}
```

Use `notify.exe Update --Id <id> --Json <file>` or `--Stdin` for a patch. An omitted field is retained; `null` explicitly clears a nullable field. `--Actions ""` and `--Items ""` empty their lists. `--Progress` updates only Value.

For pipe polling, send Get and inspect the matching occurrence's `result` in `entries[].occurrences`. For a push result, the caller first creates its own named-pipe server and sets `callbackPipe` in the request. The host sends the same stored result as one BOM-free UTF-8 JSON line. Callback delivery is best effort; the journal retains the result if the recipient is unavailable. `notify.exe --Wait` waits for the accepted occurrence.

## Scoped history removal

NotificationOperation.ClearHistory removes completed occurrences while preserving pending/future displays. Optional Id and OccurrenceId restrict removal to one series or result. The older Clear operation continues to remove completed series. The panel uses scoped history removal so deleting a past result does not cancel its recurring series.

## Data and source definitions

`UserData` contains settings, notification history, language files, themes, and templates. `AppCache` contains logs and disposable renderer data. Move the whole program folder to retain its data. Stored references to files inside the program root are relative and resolve against its new location; external files retain their absolute paths. Bare PATH command names remain unchanged. Wire requests still carry the caller's absolute file references. Failed journal commits reject the operation and restore the last committed in-memory state.

The field reference is the library's IntelliSense XML plus [contracts](https://github.com/muhammetozeski/UsefulNotification/blob/main/src/UsefulNotification.Core/Contracts.cs), [client](https://github.com/muhammetozeski/UsefulNotification/blob/main/src/UsefulNotification.Core/NotificationClient.cs), [actions](https://github.com/muhammetozeski/UsefulNotification/blob/main/src/UsefulNotification.Core/Actions.cs), and [recurrences](https://github.com/muhammetozeski/UsefulNotification/blob/main/src/UsefulNotification.Core/Recurrences.cs). To change the application itself, continue with [the development guide](Development.md).

A normal-session host was verified with real normal and elevated sender processes from the same Windows account. Both received accepted receipts through the same endpoint.
