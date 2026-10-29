using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.VisualBasic.FileIO;

namespace UsefulNotification;

/// <summary>Owns editable theme definitions and persists parent identities without flattening inherited patches.</summary>
internal static class ThemeCatalog
{
    static readonly Dictionary<string, string> Paths = new(StringComparer.OrdinalIgnoreCase);
    static readonly Dictionary<string, string> Aliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Blue"] = "builtin:daily", ["Info"] = "builtin:daily", ["Emerald"] = "builtin:success", ["Happy"] = "builtin:success",
        ["Amber"] = "builtin:attention", ["Warn"] = "builtin:attention", ["Crimson"] = "builtin:urgent", ["Error"] = "builtin:urgent"
    };
    static List<NotificationTheme> LegacyThemes = NotificationThemes.Legacy;
    public static long Revision;
    public static List<NotificationTheme> Themes = NotificationThemes.All;
    /// <summary>Useful definitions shown in the theme hierarchy, including saved user children.</summary>
    public static List<NotificationTheme> PrimaryThemes => Themes;
    /// <summary>Neutral black panel appearance independent of the selected notification definition.</summary>
    public static NotificationTheme PanelTheme { get; } = new()
    {
        Id = "panel", Name = "Panel",
        Palette = new()
        {
            Background = System.Drawing.ColorTranslator.FromHtml("#191919"), Surface = System.Drawing.ColorTranslator.FromHtml("#242424"),
            LightBackground = System.Drawing.ColorTranslator.FromHtml("#f7f7f7"), DarkBackground = System.Drawing.ColorTranslator.FromHtml("#191919"),
            LightAccent = System.Drawing.ColorTranslator.FromHtml("#2563eb"), DarkAccent = System.Drawing.ColorTranslator.FromHtml("#a4c6ff")
        },
        Typography = new() { Body = 16, Small = 13, Heading = 24, CardTitle = 20 },
        Layout = new() { ButtonHeight = 38, Radius = 8, Padding = 14, PagePadding = 24, SidebarWidth = 180 }
    };

    /// <summary>Loads compact parent-reference documents and binds every child to its single shared catalog parent.</summary>
    public static void Load()
    {
        string directory = Path.Combine(AppPaths.UserData, AppConstants.Storage.ThemesDirectory);
        Directory.CreateDirectory(directory);
        var definitions = NotificationThemes.All.Concat(NotificationThemes.Legacy).Select(theme => NotificationProtocol.Copy(theme)).ToDictionary(theme => theme.Id, StringComparer.OrdinalIgnoreCase);
        var defaults = new Dictionary<string, NotificationTheme>(definitions, StringComparer.OrdinalIgnoreCase);
        var parents = definitions.Values.ToDictionary(theme => theme.Id, theme => theme.Parent?.Id, StringComparer.OrdinalIgnoreCase);
        var paths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (string path in Directory.EnumerateFiles(directory, "*.json").OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase))
        {
            var loaded = RunSafely(() =>
            {
                JsonObject document = JsonNode.Parse(File.ReadAllText(path))?.AsObject() ?? throw new JsonException("A theme object is required.");
                string? parent = document["parent"] is JsonValue reference ? reference.GetValue<string>() : document["parent"]?["id"]?.GetValue<string>();
                document.Remove("parent");
                NotificationTheme theme = document.Deserialize<NotificationTheme>(AppConstants.StorageJson) ?? throw new JsonException("A theme object is required.");
                if (!document.ContainsKey("id")) theme.Id = definitions.Values.FirstOrDefault(item => item.Name.Equals(theme.Name, StringComparison.OrdinalIgnoreCase))?.Id ?? "legacy:" + Path.GetFileNameWithoutExtension(path);
                ArgumentException.ThrowIfNullOrWhiteSpace(theme.Id);
                ArgumentException.ThrowIfNullOrWhiteSpace(theme.Name);
                if (paths.ContainsKey(theme.Id)) throw new ArgumentException($"Duplicate theme identity '{theme.Id}' in '{path}'.");
                if (definitions.Values.Any(existing => !existing.Id.Equals(theme.Id, StringComparison.OrdinalIgnoreCase) && existing.Name.Equals(theme.Name, StringComparison.OrdinalIgnoreCase)))
                    throw new ArgumentException($"Duplicate theme name '{theme.Name}' in '{path}'.");
                theme.Validate();
                return (Theme: theme, Parent: parent);
            });
            if (!loaded.Succeeded) continue;
            definitions[loaded.Value.Theme.Id] = loaded.Value.Theme;
            parents[loaded.Value.Theme.Id] = loaded.Value.Parent;
            paths[loaded.Value.Theme.Id] = path;
        }
        bool removed;
        do
        {
            removed = false;
            foreach (NotificationTheme theme in definitions.Values)
                theme.Parent = parents[theme.Id] is { } parent && definitions.TryGetValue(parent, out NotificationTheme? match) ? match : null;
            foreach (NotificationTheme theme in definitions.Values.ToArray())
            {
                var validation = RunSafely(() =>
                {
                    if (parents[theme.Id] is { } parent && !definitions.ContainsKey(parent)) throw new ArgumentException($"Missing parent theme: {parent}");
                    ThemeResolver.ValidateChain(theme);
                    _ = ThemeResolver.ResolveTheme(theme);
                });
                if (validation.Succeeded) continue;
                if (!paths.Remove(theme.Id)) throw new InvalidDataException("A built-in theme definition is invalid.");
                if (defaults.TryGetValue(theme.Id, out NotificationTheme? original)) { definitions[theme.Id] = original; parents[theme.Id] = original.Parent?.Id; }
                else { definitions.Remove(theme.Id); parents.Remove(theme.Id); }
                removed = true;
            }
        } while (removed);
        Themes = definitions.Values.Where(theme => !NotificationThemes.Legacy.Any(legacy => legacy.Id == theme.Id)).ToList();
        LegacyThemes = definitions.Values.Where(theme => NotificationThemes.Legacy.Any(legacy => legacy.Id == theme.Id)).ToList();
        Paths.Clear(); foreach (var item in paths) Paths.Add(item.Key, item.Value);
        Interlocked.Increment(ref Revision);
    }

    /// <summary>Resolves names and stable identities at text boundaries; C# callers pass actual definitions.</summary>
    /// <param name="name">Optional text identity; null uses the host notification selection.</param>
    /// <param name="required">True reloads and rejects an unknown identity; false falls back to Daily.</param>
    /// <returns>The actual shared catalog definition.</returns>
    public static NotificationTheme Get(string? name = null, bool required = false)
    {
        string identity = name ?? Settings.Theme.Value;
        NotificationTheme? selected = Find(identity);
        if (selected is null && required) { Load(); selected = Find(identity); }
        return selected ?? (required ? throw new ArgumentException($"Unknown notification theme: {identity}") : Themes.FirstOrDefault(theme => theme.Id == "builtin:daily") ?? NotificationThemes.Daily);
    }

    /// <summary>Uses the accepted detached appearance when available and resolves drafts otherwise.</summary>
    /// <param name="request">Accepted request or detached editor request.</param>
    /// <returns>The concrete appearance belonging to this request.</returns>
    public static NotificationTheme For(NotificationRequest request) => request.Resolved?.Theme ?? ThemeResolver.Resolve(request).Theme;

    /// <summary>Creates and saves a child with an empty patch so all fields inherit from its actual parent.</summary>
    /// <param name="name">Unique nonblank display name.</param>
    /// <param name="parent">Actual catalog parent; null selects Daily.</param>
    /// <returns>The new shared definition.</returns>
    public static NotificationTheme Create(string name, NotificationTheme? parent = null) => Save(new() { Name = name, Parent = parent ?? Get("Daily", true) });

    /// <summary>Validates a detached draft, persists its own patch and updates its shared catalog identity in place.</summary>
    /// <param name="draft">Detached definition whose Parent is rebound by stable identity.</param>
    /// <returns>The shared catalog object used by all its children.</returns>
    public static NotificationTheme Save(NotificationTheme draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        ArgumentException.ThrowIfNullOrWhiteSpace(draft.Name);
        NotificationTheme captured = ThemeResolver.CreateDraft(draft);
        if (captured.Parent is { } parent)
            captured.Parent = Find(parent.Id) ?? throw new ArgumentException($"Missing parent theme: {parent.Id}");
        if (Themes.Concat(LegacyThemes).Any(theme => theme.Id != captured.Id && theme.Name.Equals(captured.Name, StringComparison.OrdinalIgnoreCase)))
            throw new ArgumentException("A theme with that name already exists.");
        ThemeResolver.ValidateChain(captured);
        _ = ThemeResolver.ResolveTheme(captured);
        JsonObject document = JsonSerializer.SerializeToNode(captured, AppConstants.StorageJson)!.AsObject();
        if (captured.Parent is { } actualParent) document["parent"] = actualParent.Id;
        string directory = Path.Combine(AppPaths.UserData, AppConstants.Storage.ThemesDirectory);
        Directory.CreateDirectory(directory);
        string path = Paths.GetValueOrDefault(captured.Id) ?? Path.Combine(directory, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(captured.Id)))[..24] + ".json");
        PortableStore.Write(Path.GetRelativePath(AppPaths.UserData, path), document);
        NotificationTheme? existing = Find(captured.Id);
        if (existing is null) { existing = captured; Themes.Add(existing); }
        else CopyDefinition(existing, captured);
        Paths[captured.Id] = path;
        Interlocked.Increment(ref Revision);
        return existing;
    }

    /// <summary>Removes a user definition after ensuring no child still inherits it; the saved document goes to the Recycle Bin.</summary>
    /// <param name="theme">Actual or detached user definition identified by Id.</param>
    public static void Delete(NotificationTheme theme)
    {
        if (theme.Id.StartsWith("builtin:", StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("A built-in theme cannot be deleted.");
        if (Themes.Concat(LegacyThemes).Any(item => item.Parent?.Id == theme.Id)) throw new ArgumentException("A theme with children cannot be deleted.");
        if (Paths.TryGetValue(theme.Id, out string? path) && File.Exists(path))
            FileSystem.DeleteFile(path, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin);
        Themes.RemoveAll(item => item.Id == theme.Id); Paths.Remove(theme.Id);
        Interlocked.Increment(ref Revision);
    }

    /// <summary>Finds a stable identity, editable name or legacy text alias in the loaded catalog.</summary>
    static NotificationTheme? Find(string identity)
    {
        string key = Aliases.GetValueOrDefault(identity) ?? identity;
        return Themes.Concat(LegacyThemes).FirstOrDefault(theme => theme.Id.Equals(key, StringComparison.OrdinalIgnoreCase) || theme.Name.Equals(key, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Preserves the shared identity object while replacing only its editable definition data.</summary>
    static void CopyDefinition(NotificationTheme target, NotificationTheme source)
    {
        target.Name = source.Name; target.Parent = source.Parent; target.Overrides = source.Overrides;
        target.Palette = source.Palette; target.Typography = source.Typography; target.Layout = source.Layout; target.Frame = source.Frame; target.DefaultLevel = source.DefaultLevel;
        target.HasPalette = source.HasPalette; target.HasTypography = source.HasTypography; target.HasLayout = source.HasLayout; target.HasFrame = source.HasFrame;
    }
}
