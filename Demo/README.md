# UsefulNotification interactive demo

The demo is an external consumer of the public Core API. It does not change broker internals. Select a scenario, read its instructions and expected result, inspect or edit its code, and run that exact code through the PowerShell/C# compiler flow.

`Catalog.json` supplies Turkish and English labels and instructions. Its `File` references a body under `Examples`. The runner supplies ordinary .NET/UsefulNotification imports, the configured `NotificationClient.Options`, a cancellation token named `token`, a `DirectoryInfo` named `assets`, and `Print(object)` for the output pane. `Print` reports receipts/results; it is display plumbing rather than an alternate notification API.

The examples cover the six complete built-in themes and six compatibility themes, structured custom appearance, content, action types, built-in sound overrides and custom sound, all recurrence policies, partial updates, stable resend/tag behavior, callbacks, query/management, cancellation and invalid requests. Every calendar recurrence first appears after three seconds; dismiss it and inspect its next due date instead of waiting until tomorrow or next year.

Run interactive examples individually. A pending result waits for your actual notification action. The demo's Stop control cancels that runner's wait; it does not imply that the notification was dismissed. Use the demo cleanup control for notifications created by the demo. Scheduled and recurring examples remain until dismissed or explicitly cleaned up.

Sound examples are marked audible and honor the broker's sound setting. The URL and command examples open a browser/Notepad only after you press the corresponding notification button. The shutdown example explicitly stops UsefulNotification; a later send can start it again.

Management examples target their own notification identity. List is read-only. The demo does not issue a global history clear or remove unrelated notifications. Settings changed in the real application panel are actual preferences.

Assets are local: `sample.svg`, a generated short PCM `sample.wav`, and the sandboxed `reply-card.html`. No download is required. The HTML example declares its reply action in the request and posts only that action identifier and reply to the parent renderer.

For the public API contract and source modification points, read [Integration](../Docs/Integration.md), [Appearance](../Docs/Appearance.md), and [Development](../Docs/Development.md).

## Build and launch

The GUI and every example are compiled directly by the installed Roslyn csc.dll through Build.ps1. The demo requires Windows x64, .NET 10 Desktop Runtime, the .NET 10 SDK with desktop reference assemblies, and PowerShell 7. No package restore or downloads are performed by the demo.

Create the application publish with the external NewTemp output described in [Build](../Docs/Build.md). From the repository root, run `pwsh -NoProfile -File Demo\Build.ps1 -PackageRoot "C:\C\UsefulNotificationPublish\UsefulNotification"`, substituting the actual publish folder returned by that workflow. It adds UsefulNotification.Demo.exe and its source catalog to the existing publish folder. Launch that EXE beside UsefulNotification.exe; select the installed host at the top if extracted elsewhere. The UI provides 57 examples, editable code, instructions, expected results, real stdout/stderr, Stop, saved user overrides, and demo-only cleanup. F5 runs the selected body. Copy complete code includes the real runner context. Saved overrides live under UserData/Demo/Examples; generated code and logs live under AppCache/Demo.

## Compose a notification without compilation

Open the demo and press **Compose notification** above the code examples. Select Base, Daily, Success, Attention, Urgent, Reminder, or Workshop, an example theme inherited from Success. Each theme supplies its colors, sound, lifetime, acknowledgement and queue priority together. The optional accent, sound, priority, lifetime and acknowledgement groups initially inherit their theme values. Silent explicitly overrides sound with null. Changing the theme retains only overrides you actually selected.

The color preview and effective summary use the same public `ThemeResolver` as the host. The summary shows the resolved sound, lifetime, priority and acknowledgement, including whether Snooze is available. The JSON below it is the exact public request submitted by the Send control.

**Send notification** starts the existing notify.exe beside the selected host, writes the displayed JSON to its standard input, and shows its actual response or error. This path requires the installed .NET 10 Desktop Runtime and the notification application. The editable code examples retain their compiler requirements. Notifications from this window use the existing demo sender identity so **Delete demo notifications** also removes them.
