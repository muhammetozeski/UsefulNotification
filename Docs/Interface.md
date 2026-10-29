# Panel interaction and presentation

The panel separates pending notifications, future schedules, and completed history. Sounds and settings are separate work areas. Lists have labeled search/filter/sort controls, result counts, and bounded paging. Filtering resets the page; journal changes clamp it to the available results. Manual movement is shown only with manual ordering.

The main action creates a notification. Quick reminder buttons open an editable scheduled draft. Editing replaces the list. Basic content, scheduling, and advanced appearance/behavior fields are grouped separately. Typed actions and checklist items use identity-based rows: removing or renaming one row never remaps a surviving action's program, URI, or identifier.

The panel owns the leave decision for unsaved edits. Navigation, new/edit, cancellation, closing, and quitting use the same guard. Cancel retains the draft; discarding clears editor ownership before leaving. A modal decision traps Tab focus and supports Escape. Reloading settings also refreshes the visible draft.

History shows the completed occurrence's stored reply and checked items. Deleting history preserves active/future occurrences of a recurring series. Editing history creates a new draft; resending creates a separate notification. Other sections manage the existing series. Sound editing replaces only sound data, retaining current panel placement.

## Moving and resizing the HTML window

The window follows ShowPicOnly's explicit down/move/up model. Pointer-down captures the real cursor and original window rectangle. While the left button remains held, WindowHost applies the cursor delta through AppWindow.Move or MoveAndResize. Pointer release, cancellation, capture loss, window blur, minimization, and panel closure end ownership. Queued animation callbacks also check that their interaction remains current.

This replaces both synchronous and posted Windows modal move messages. In user testing, the posted-message attempt moved after release and required a second click to stop. The owned pointer state keeps release in the same interaction path as movement. Minimum dimensions are applied during resizing, and saved placement is clamped to an available screen.

Native caption/frame styles are removed. WinUI can retain an eight-pixel nonclient inset even after those styles change; the window subclass returns a full client rectangle for WM_NCCALCSIZE. The visible shell and its border remain HTML. Only initial preparation or an explicit Show opens the panel; journal renders preserve minimization. Show restores a minimized panel. Close retains the broker; Quit is explicit.

For this Windows App SDK runtime, copy DisplayArea.FindAll through Count/index access before managed enumeration. Direct LINQ enumeration of its projected collection produced InvalidCastException during saved-placement restoration and prevented startup. WindowHost.Monitors and RestorePanelBounds keep this boundary.

## Appearance and evidence

C# theme objects own colors, typography, layout, backdrop and card frame. Primary themes supply coordinated colors, sound and delivery. The card uses a quiet frame with an inward accent glow and light/dark values that follow the system appearance. Click acknowledgement leaves input controls, links, actions and Snooze with their own interaction. The snooze chooser is in measured document flow so the toast can grow without clipping it.

Disposable checks exercise actual Razor editor handlers for row identity, independent drafts, discard/cancel, filtering and scoped history deletion. Production components are also server-rendered for visual inspection. Pointer lifecycle checks exercise down/move/release, queued movement after release, cancellation, capture loss, blur and secondary clicks. These checks complement user mouse feedback on the running native application.
The client uses a distinct adjustable response budget so the normal 2,000-entry history can be listed without relaxing the host request limit. Send acceptance links are retained with bounded history, including source/tag upserts, so a retry does not replace newer content or create another display.
