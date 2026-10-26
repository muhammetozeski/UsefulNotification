# UsefulNotification interactive demo

The demo is an external consumer of the public Core API. It does not change broker internals. Select a scenario, read its instructions and expected result, inspect or edit its code, and run that exact code through the PowerShell/C# compiler flow.

`Catalog.json` supplies Turkish and English labels and instructions. Its `File` references a body under `Examples`. The runner supplies ordinary .NET/UsefulNotification imports, the configured `NotificationClient.Options`, a cancellation token named `token`, a `DirectoryInfo` named `assets`, and `Print(object)` for the output pane. `Print` reports receipts/results; it is display plumbing rather than an alternate notification API.

The examples cover all twelve built-in themes, structured custom appearance, content, action types, four sound presets and custom sound, all recurrence policies, partial updates, stable resend/tag behavior, callbacks, query/management, cancellation and invalid requests. Every calendar recurrence first appears after three seconds; dismiss it and inspect its next due date instead of waiting until tomorrow or next year.

Run interactive examples individually. A pending result waits for your actual notification action. The demo's Stop control cancels that runner's wait; it does not imply that the notification was dismissed. Use the demo cleanup control for notifications created by the demo. Scheduled and recurring examples remain until dismissed or explicitly cleaned up.

Sound examples are marked audible and honor the broker's sound setting. The URL and command examples open a browser/Notepad only after you press the corresponding notification button. The shutdown example explicitly stops UsefulNotification; a later send can start it again.

Management examples target their own notification identity. List is read-only. The demo does not issue a global history clear or remove unrelated notifications. Settings changed in the real application panel are actual preferences.

Assets are local: `sample.svg`, a generated short PCM `sample.wav`, and the sandboxed `reply-card.html`. No download is required. The HTML example declares its reply action in the request and posts only that action identifier and reply to the parent renderer.

For the public API contract and source modification points, read [Integration](../Docs/Integration.md), [Appearance](../Docs/Appearance.md), and [Development](../Docs/Development.md).

## Build and launch

The GUI and every example are compiled directly by the installed Roslyn csc.dll through Build.ps1. The demo requires Windows x64, .NET 10 Desktop Runtime, the .NET 10 SDK with desktop reference assemblies, and PowerShell 7. No package restore or downloads are performed by the demo.

From the repository root, run `pwsh -NoProfile -File Demo\Build.ps1`. It adds UsefulNotification.Demo.exe and its source catalog to the existing publish folder. Launch that EXE beside UsefulNotification.exe; select the installed host at the top if extracted elsewhere. The UI provides 57 examples, editable code, instructions, expected results, real stdout/stderr, Stop, saved user overrides, and demo-only cleanup. F5 runs the selected body. Copy complete code includes the real runner context. Saved overrides live under UserData/Demo/Examples; generated code and logs live under AppCache/Demo.

All 57 source bodies were compiled with warnings treated as errors. A management body was compiled and executed against the actual resident host; its own get/replace/move/dismiss/retry/snooze/history/delete calls returned accepted results. Interactive buttons, replies and audible examples are left for the person using the demo.