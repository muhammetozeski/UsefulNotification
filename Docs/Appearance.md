# HTML, themes, and frames

UsefulNotification removes the native Windows caption and border. The visible card and its frame are HTML. C# theme objects generate the shipped CSS, including colors, typography, spacing, and borders. The shipped stylesheet has no parallel CSS custom-property system.

The default toast width is 392 physical pixels. Content determines height. The native host compensates for display scaling; geometry policies are internal application settings in `AppConstants.Windows` for source developers. Theme dimensions are logical CSS pixels.

## Use or extend an actual theme

C# callers choose complete objects such as `NotificationThemes.Info`, `NotificationThemes.Warn`, `NotificationThemes.Error`, and `NotificationThemes.Happy`. The other built-ins are Dark, Light, Blue, Emerald, Purple, Amber, Crimson, and HighContrast. Semantic themes supply their level when the request omits one.

```csharp
using System.Drawing;
using UsefulNotification;

var theme = NotificationThemes.Error with
{
    Frame = NotificationThemes.Error.Frame with
    {
        Pattern = BorderPattern.Dashed,
        CornerRadius = 0,
        AccentEdges = BorderEdges.All,
        AccentColor = Color.Firebrick,
        Thickness = new() { Top = 2, Right = 2, Bottom = 2, Left = 5 }
    }
};

await NotificationClient.SendAsync(new()
{
    Title = "Build failed",
    Message = "Open the build log for details.",
    Theme = theme
});
```

Presets are mutable objects. A record `with` expression makes a shallow copy: copy the nested object you intend to edit, as above, to keep the shared preset unchanged. Send captures a detached payload before waiting for the connection. A later preset edit affects subsequent sends.

`NotificationAppearance` can replace a request's Palette, Typography, Layout, or Frame independently. Each supplied section is a complete section with its model defaults; it is not a property-by-property merge into the selected section. Ordinary cards receive scoped CSS, so one sender's appearance does not change its neighbours.

## File-backed themes for scripts and the panel

Place a `NotificationTheme` JSON document under `UserData\Themes` in the portable application folder. Use Reload after editing. The theme contains `palette`, `typography`, `layout`, and `frame` objects. See the actual [theme models](https://github.com/muhammetozeski/UsefulNotification/blob/main/src/UsefulNotification.Core/Themes.cs) and their IntelliSense descriptions for all fields.

```json
{
  "name": "Workshop",
  "frame": {
    "thickness": { "top": 2, "right": 2, "bottom": 2, "left": 6 },
    "cornerRadius": 0,
    "pattern": "solid",
    "accentEdges": "all",
    "accentColor": "#70a5cf"
  }
}
```

Omitted sections use model defaults. Colors accept HTML color names or `#RRGGBB`; serialized colors use `#RRGGBBAA`, with alpha last. A loaded theme with an existing name replaces that name in the host's file-backed catalog.

Select this theme at the text boundary:

```powershell
.\notify.exe "Workshop" "The report is ready." --theme Workshop
```

CLI and JSON theme names are resolved case-insensitively by the host. C# callers supply their complete `NotificationTheme` object directly.

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
.\notify.exe "Deployment complete" "The service is available." --type Custom
.\notify.exe "Report ready" "The report is available." --template "C:\MyProgram\Notifications\report.html"
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
