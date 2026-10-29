# Changing the application

Read [integration](Integration.md) for the public contract and [appearance](Appearance.md) for card design. This guide follows a request through the program so a change can be placed at its actual owner. Source links open the repository and also work from the release package, whose payload contains binaries rather than source projects.

## Request to result

1. The caller references the Core NuGet package and gives `NotificationClient` a typed request. The client captures its serialized payload before the first connection wait, then connects to the shared local endpoint. If startup is needed, an explicit `Application` takes precedence over account registration and an adjacent host payload. CLI and direct-pipe JSON inputs pass through the host's text compatibility boundary.
2. `NotificationHub.Apply` copies and validates the request, updates its series/occurrence, and commits the journal. Only then does it return an accepted receipt and publish the new view.
3. The scheduler reserves display capacity for due entries. Each reservation records a delivery start; queued entries have no running expiry timer.
4. The toast `Surface` renders those entries. `NotificationCard` builds standard markup or a sandboxed HTML iframe. Native `WindowHost` waits for prepared content and sizes its resident window.
5. `surface.js` reports each card's viewport intersection with its occurrence and content revision. `WindowHost` adds native visibility checks. `NotificationHub.ConfirmVisible` accepts matching evidence, records delivery, starts the lifetime, and requests sound after the commit.
6. A current control submits its interaction to the hub. Internal completion or a persisted external-action attempt owns the result. External effects happen outside the journal lock; their recorded outcome is published through the same stored result used by polling, callbacks, and history.

This separation keeps communication independent of the renderer and prevents a stale render or click from acknowledging a later display.

## Put a change at its owner

| Change | Owner and reason |
| --- | --- |
| Public request, update, receipt, or result | [Contracts.cs](https://github.com/muhammetozeski/UsefulNotification/blob/main/src/UsefulNotification.Core/Contracts.cs): document defaults, units, null, and ownership; preserve patch field presence |
| Client endpoint or connection behaviour | [NotificationClient.cs](https://github.com/muhammetozeski/UsefulNotification/blob/main/src/UsefulNotification.Core/NotificationClient.cs): the caller's options and request policies |
| Portable host discovery | [HostDiscovery.cs](https://github.com/muhammetozeski/UsefulNotification/blob/main/src/UsefulNotification.Core/HostDiscovery.cs): register the successfully initialized launcher for its Windows account and resolve it without caller-specific paths |
| Supported action | [Actions.cs](https://github.com/muhammetozeski/UsefulNotification/blob/main/src/UsefulNotification.Core/Actions.cs): required data, validation, and execution belong with the action object |
| Repeated schedule | [Recurrences.cs](https://github.com/muhammetozeski/UsefulNotification/blob/main/src/UsefulNotification.Core/Recurrences.cs): calculate from the stable series anchor |
| Queue, lifecycle, or durable interaction | [NotificationHub.cs](https://github.com/muhammetozeski/UsefulNotification/blob/main/src/UsefulNotification.Core/NotificationHub.cs): one state owner and completion route |
| Legacy text input or wire shape | [NotificationProtocol.cs](https://github.com/muhammetozeski/UsefulNotification/blob/main/src/UsefulNotification.Core/NotificationProtocol.cs): name/file lookup stays at this boundary |
| Journal replacement and backup recovery | [PortableStore.cs](https://github.com/muhammetozeski/UsefulNotification/blob/main/src/UsefulNotification.Core/PortableStore.cs): propagate commit failure to the hub |
| Theme defaults, inherited overrides or structured frame | [Themes.cs](https://github.com/muhammetozeski/UsefulNotification/blob/main/src/UsefulNotification.Core/Themes.cs): actual reusable objects generate presentation values; [ThemeResolution.cs](https://github.com/muhammetozeski/UsefulNotification/blob/main/src/UsefulNotification.Core/ThemeResolution.cs) resolves parent objects and per-field overrides into accepted snapshots |
| Standard layout or custom HTML | [Appearance guide](Appearance.md): markup, generated CSS, template bridge, and cache owners |
| Placement, border removal, tray, sound | [WindowHost.cs](https://github.com/muhammetozeski/UsefulNotification/blob/main/src/UsefulNotification/WindowHost.cs): native Windows effects stay outside the journal lock |
| Panel and editors | [Components](https://github.com/muhammetozeski/UsefulNotification/tree/main/src/UsefulNotification/Components): edit detached drafts and save through the owning service |
| Identity and internal policies | [AppConstants.cs](https://github.com/muhammetozeski/UsefulNotification/blob/main/src/UsefulNotification.Core/AppConstants.cs), [ProductOptions.cs](https://github.com/muhammetozeski/UsefulNotification/blob/main/src/UsefulNotification.Core/ProductOptions.cs): keep application decisions out of the public client surface |

Use existing theme, action, lifetime, and recurrence objects. A string or enum-to-object lookup would split configuration from the object developers need. Ordinary finite data such as status or queue priority remains an enum. User text, business identifiers, HTML, paths in text input, and protocol property names retain their actual textual role.

## Preserve the ownership boundaries

The hub owns mutable accepted entries. Before acceptance it resolves the actual theme-parent chain and per-request overrides into a detached `ResolvedNotification` snapshot. Rendering, sound, queue priority, lifetime and interaction consume that snapshot. A commit produces a detached published view used by renderers; renderers consume that view and pass operations back to the hub. Editor drafts and sound/preferences drafts remain independent until validation and persistence succeed. Mutable built-in presets are reusable definitions; they must not become live aliases of accepted requests.

`HostDiscovery` keeps the account's current portable launcher path outside any caller project. Host startup refreshes the `Executable` value under `HKEY_CURRENT_USER\Software\UsefulNotification`; moving the application requires launching it from the new location. The endpoint remains shared across caller directories. The host runs in the user's normal Windows session. `NotificationHub.ListenAsync` creates the pipe with a protected permission list owned by the current account, grants that account full access, and denies network access. `NotificationClient` verifies the pipe owner and requests identification permission. Keep discovery and transport permission decisions together with these owners.

An ordinary update keeps the current occurrence and changes its content revision. Rescheduling or another display creates a new occurrence, while previous results remain within retention limits. Replace edits a complete request and retains an active occurrence when its schedule is unchanged. Retry explicitly closes the old occurrence and creates an immediate one while preserving the request and series anchor. Keep these operations separate so retrying a future recurring reminder neither waits for its old due time nor resets its recurrence anchor. Render evidence and controls carry the occurrence/revision that produced them.

Lifetime begins from confirmed display rather than send time or queue reservation. A supplied lifetime update on a visible occurrence starts the new duration from that update, or removes expiry for acknowledgement; clearing the override returns to content-based defaults. A changed effective default also reapplies lifetime. Viewport/hover pause state survives this change. Delivery failure completes one occurrence and lets a recurring series calculate its next anchored occurrence. Keep expiry, acknowledgement, snooze, and failure using the shared result rules.

External actions first commit their attempt, then execute once outside the lock, then commit their outcome. Do not add automatic `Process.Start` retries: after a crash between the side effect and recorded outcome, repeating the command could duplicate work. Recovery records interruption instead. A failed pre-action journal write must prevent the external effect.

The hub owns the broker/scheduler cancellation token and supervised background work. Add sound, callbacks, or other background effects through that lifetime owner and await its shutdown. UI event handlers use the existing operation boundary; lower operations propagate a real error for the boundary to handle. Test and development tools consume these contracts without reshaping lower production layers to suit a test.

## Build, verify, and document a change

Open `UsefulNotification.slnx`, then follow [the build guide](Build.md). Its Visual Studio MSBuild and Windows resource rules preserve the established packaging path. Application publishing uses normal NuGet restore and requires a disposable output root outside the source tree. `Sdk\Build.ps1` owns packaging the Core library and its exact dependencies; `Sdk\Install.ps1` owns user-level feed registration and offline repair of the caller package cache. Consumer projects use a package reference as described in [integration](Integration.md#call-from-c).

Verify the behaviour actually changed. Meaningful lifecycle checks include partial-update preservation, explicit clearing, matching occurrence waits, stale-control rejection, journal failure before an external effect, viewport-dependent expiry, and complete shutdown. Temporary harnesses and journals belong outside the source tree. Copying or signing a verified binary does not change its behaviour and needs no duplicate behaviour test.

Update public XML comments with changed defaults, units, null semantics, and ownership. Keep generated XML beside the library. Update the relevant guide instead of maintaining a second field inventory. Put reusable, evidenced methods in Docs and a dated context/lesson in Walkthrough when it changes the next worker's approach. [Documentation guidance](Documentation.md) explains the evidence and maintenance rules.
