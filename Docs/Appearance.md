# Themes, inheritance and HTML cards

UsefulNotification removes the native Windows caption and border. The visible card and its frame are HTML. C# theme objects generate the shipped CSS, including colors, typography, spacing and borders. A complete notification theme also supplies its sound, lifetime, queue priority and acknowledgement policy.

The default toast width is 410 logical pixels. Content determines height; Windows display scaling and the personal text-scale preference remain active. Geometry policies are internal application settings in `AppConstants.Windows`, separate from the per-card appearance.

## Select or inherit an actual theme

The primary built-ins are `NotificationThemes.Base`, `Daily`, `Success`, `Attention`, `Urgent` and `Reminder`. `Base` provides the common defaults. The other presets express a complete notification purpose, including their sound and delivery. Legacy palette names remain accepted for existing callers and files.

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
        Frame = new() { CornerRadius = new(8) },
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

`Parent` is an actual `NotificationTheme` object in C#. A derived theme inherits every field it does not override. Each field in `NotificationOverrides.Palette`, `Typography`, `Layout` and `Frame` is independent. `new(value)` supplies that field; omission inherits it. `Sound = new(null)` explicitly mutes sound. Lifetime, priority and interaction support the same presence-aware override model.

A simple Accent or Background override also changes the inherited paired light/dark values, unless the same override explicitly supplies those pairs. `LightBackground`, `DarkBackground`, `LightAccent` and `DarkAccent` can instead define separate values for the system color scheme. CSS follows the system appearance from those values.

Presets are mutable definitions. `ThemeResolver.Resolve(request)` returns the detached effective appearance, sound and delivery, plus `FieldOrigins` identifying which theme or request supplied each value. The host stores the accepted snapshot with the request. A later parent or preset edit affects subsequent sends. Resolution rejects parent cycles and invalid values before acceptance.

Existing `Appearance`, `Sound`, `Silent`, `Level`, `Priority` and `Lifetime` request fields remain compatibility inputs. New callers should choose one complete theme and use `Overrides` for intentional differences. The new model merges individual supplied fields; the legacy `NotificationAppearance` still replaces whole supplied sections.

| Theme | Parent | Sound | Lifetime | Queue priority |
| --- | --- | --- | --- | --- |
| Base | Root | Gentle bell (`Subtle`) | 8 seconds | Normal |
| Daily | Base | Inherited gentle bell | Inherited 8 seconds | Normal |
| Success | Daily | Gentle positive two notes (`Completed`) | 6 seconds | Normal |
| Attention | Daily | Clear twin tones (`DoubleAlert`) | 12 seconds | Normal |
| Urgent | Attention | Exactly three long fixed-pitch beeps (`Attention`) | Until acknowledged | High |
| Reminder | Daily | Warm plucked tones (`Reminder`) | Until acknowledged | Normal |

All six initially allow card-background acknowledgement and retain Snooze. Content requiring input or progress can select the content-aware interaction and lifetime when those values remain inherited from Base. Sounds honor the application's master sound preference.
## File-backed themes for scripts and the panel

Place a theme JSON document under `UserData\Themes` in the portable application folder. Use Reload after editing. A stored parent is the referenced theme's stable ID; the C# library uses the actual parent object. Names are editable display labels. The catalog resolves inheritance before accepting notifications and reports unresolved parents, duplicate IDs and parent cycles.

```json
{
  "id": "workshop",
  "name": "Workshop",
  "parent": "builtin:success",
  "overrides": {
    "palette": { "accent": "#70a5cf" },
    "frame": { "cornerRadius": 8 },
    "sound": null,
    "lifetime": { "mode": "acknowledge" }
  }
}
```

Colors accept HTML color names or `#RRGGBB`; serialized colors use `#RRGGBBAA`, with alpha last. Select a loaded theme at the text boundary:

```powershell
.\notify.exe "Workshop" "The report is ready." --Theme Workshop
```

CLI and JSON theme names are resolved case-insensitively by the host. `--Theme` selects appearance, sound and delivery together. `--Silent`, `--Sound`, `--Duration`, `--Sticky` and `--Priority` add explicit per-send overrides. `--BodyClick` enables background-click acknowledgement while preserving Snooze. C# callers pass actual theme and interaction objects.
## Replace the complete card with HTML

The host creates its editable default `UserData\Templates\Card.html` on first launch and preserves existing user files. Use the default or a specific HTML file:

```csharp
await NotificationClient.SendAsync(new()
{
    Title = "Deployment complete",
    Template = new HtmlTemplateContent()
});

await NotificationClient.SendAsync(new()
{
    Title = "Report ready",
    Template = new HtmlTemplateContent(
        new FileInfo(@"C:\MyProgram\Notifications\report.html")),
    Actions = [new ReplyAction { Id = "confirm", Label = "Confirm" }]
});
```

Equivalent script calls:

```powershell
.\notify.exe "Deployment complete" "The service is available." --Type Custom
.\notify.exe "Report ready" "The report is available." --Template "C:\MyProgram\Notifications\report.html"
```

HTML supplies the entire card, including its frame and controls. The active template runs in a sandboxed iframe with scripts enabled. Completed history previews disable scripts. The parent measures the frame and resizes the stack when content changes.

Available substitutions are `{{title}}`, `{{message}}`, `{{source}}`, `{{time}}`, `{{id}}`, and `{{data.key}}` for entries in the request's Data dictionary. These values are HTML-escaped. `{{themeCss}}` supplies generated CSS and `{{frameCss}}` supplies the effective frame style. `{{dismissLabel}}` and `{{dismissAction}}` provide the localized standard dismissal control.

```html
<button onclick="parent.postMessage({action:'{{dismissAction}}'}, '*')">{{dismissLabel}}</button>
```

To submit a declared reply action:

```html
<input id="answer">
<button onclick="parent.postMessage({action:'confirm',reply:document.getElementById('answer').value}, '*')">Confirm</button>
```

The request must declare a `ReplyAction` whose Id is `confirm`. The bridge identifies the sending iframe, then supplies its notification ID, occurrence ID, and content revision to the host. The host accepts only the reserved dismissal or an action declared by that request. HTML sends the user's selection to UsefulNotification; the caller receives a structured `NotificationResult` through polling or its callback pipe.

External commands and links belong in typed action objects. A template does not specify an arbitrary program command in its posted message. Keep business data in the request and presentation in the template. If a substitution enters a JavaScript string rather than HTML text, encode it for that JavaScript context or read it from a text/attribute node.

## Rendering and modification points

Template source and local image data use bounded caches keyed by file modification time; Reload also invalidates theme-dependent views. Theme CSS stays in memory. The native toast window and Blazor surfaces stay resident between requests, so new notifications reuse them.

Change [ThemeStyle](https://github.com/muhammetozeski/UsefulNotification/blob/main/src/UsefulNotification/ThemeStyle.cs) for generated standard-card CSS and template substitution, [NotificationCard](https://github.com/muhammetozeski/UsefulNotification/blob/main/src/UsefulNotification/Components/NotificationCard.razor) for ordinary markup, and [surface.js](https://github.com/muhammetozeski/UsefulNotification/blob/main/src/UsefulNotification/wwwroot/surface.js) for viewport measurement and the iframe bridge. [The development guide](Development.md) connects these pieces to persistence and native windows.
