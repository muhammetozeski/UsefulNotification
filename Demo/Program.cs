using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;
using UsefulNotification;

[assembly: AssemblyTitle("UsefulNotification Demo")]
[assembly: AssemblyVersion("1.0.0.0")]

/// <summary>Starts an external consumer of the public SDK; the broker source and state models are not modified.</summary>
internal static class DemoProgram
{
    /// <summary>Uses standard Windows movement and DPI scaling for the code-and-result demo window.</summary>
    [STAThread] static void Main()
    {
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2); Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
        try { Application.Run(new DemoWindow()); }
        catch (Exception error) { MessageBox.Show(error.ToString(), "UsefulNotification Demo", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }
}

/// <summary>Pairs runnable source with the exact user interaction and expected result in both shipped languages.</summary>
internal sealed record Example
{
    public string Id { get; init; } = string.Empty;
    public string Category { get; init; } = string.Empty;
    public string CategoryEn { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string TitleEn { get; init; } = string.Empty;
    public string Instructions { get; init; } = string.Empty;
    public string InstructionsEn { get; init; } = string.Empty;
    public string Expected { get; init; } = string.Empty;
    public string ExpectedEn { get; init; } = string.Empty;
    public string File { get; init; } = string.Empty;
    public bool Interactive { get; init; }
    public bool Audible { get; init; }
}

/// <summary>Owns the demo's paths and native control appearance rather than changing production theme objects.</summary>
internal sealed class DemoOptions
{
    public string Root = Path.GetFileName(Path.TrimEndingDirectorySeparator(AppContext.BaseDirectory)).Equals("AppData", StringComparison.OrdinalIgnoreCase) ? Directory.GetParent(Path.TrimEndingDirectorySeparator(AppContext.BaseDirectory))!.FullName : AppContext.BaseDirectory;
    public Font TextFont = new("Segoe UI", 10);
    public Font CodeFont = new("Consolas", 10);
    public Size Window = new(1280, 900);
    public Size MinimumWindow = new(1000, 700);
    public Padding Padding = new(12);
    public Color Accent = Color.FromArgb(40, 100, 200);
    public TimeSpan StopGrace = TimeSpan.FromSeconds(2);
    public string Demo => Path.Combine(Root, "Demo");
    public string Payload => Path.Combine(Root, "AppData");
    public string Cache => Path.Combine(Root, "AppCache", "Demo");
    public string UserCode => Path.Combine(Root, "UserData", "Demo", "Examples");
    public string ExampleAssembly => "UsefulNotification.Demo.Example.dll";
}

/// <summary>Shows editable real example code, compiles it through the shipped PowerShell/csc script and displays its actual output.</summary>
internal sealed class DemoWindow : Form
{
    readonly DemoOptions Options = new();
    readonly TreeView Examples = new() { Dock = DockStyle.Fill, HideSelection = false };
    readonly RichTextBox Code = new() { Dock = DockStyle.Fill, WordWrap = false, AcceptsTab = true, DetectUrls = false };
    readonly RichTextBox Output = new() { Dock = DockStyle.Fill, ReadOnly = true, WordWrap = false, DetectUrls = false };
    readonly TextBox HostPath = new() { Dock = DockStyle.Fill };
    readonly ComboBox Language = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 100 };
    readonly Label HostLabel = new() { AutoSize = true, Anchor = AnchorStyles.Left };
    readonly Label Heading = new() { AutoSize = true };
    readonly TextBox Instructions = new() { Dock = DockStyle.Fill, ReadOnly = true, Multiline = true, BorderStyle = BorderStyle.None, ScrollBars = ScrollBars.Vertical };
    readonly TextBox Expected = new() { Dock = DockStyle.Fill, ReadOnly = true, Multiline = true, BorderStyle = BorderStyle.None };
    readonly Label CodeLabel = new() { AutoSize = true };
    readonly Label OutputLabel = new() { AutoSize = true };
    readonly Label State = new() { AutoSize = true };
    readonly Button Browse = new() { AutoSize = true };
    readonly Button Run = new() { AutoSize = true };
    readonly Button Stop = new() { AutoSize = true, Enabled = false };
    readonly Button Copy = new() { AutoSize = true };
    readonly Button Save = new() { AutoSize = true };
    readonly Button Reset = new() { AutoSize = true };
    readonly Button Common = new() { AutoSize = true };
    readonly Button Cleanup = new() { AutoSize = true };
    readonly Button ClearOutput = new() { AutoSize = true };
    readonly Button Compose = new() { AutoSize = true };
    readonly List<Example> Catalog;
    Example? Selected;
    Process? CurrentProcess;
    Task? Execution;
    bool Running, IsExampleProcess, Loading, ClosingAfterStop, StopRequested;
    string? LogPath;
    string LoadedCode = string.Empty;
    bool English => Language.SelectedIndex == 1;

    /// <summary>Loads the catalog without sending any notification automatically.</summary>
    public DemoWindow()
    {
        Text = "UsefulNotification Demo"; Font = Options.TextFont; Size = Options.Window; MinimumSize = Options.MinimumWindow; StartPosition = FormStartPosition.CenterScreen;
        Code.Font = Output.Font = Options.CodeFont;
        Output.BackColor = SystemColors.Window;
        HostPath.Text = Path.Combine(Options.Root, "UsefulNotification.exe");
        Catalog = JsonSerializer.Deserialize<List<Example>>(File.ReadAllText(Path.Combine(Options.Demo, "Catalog.json"))) ?? throw new InvalidDataException("Demo catalog is empty.");
        Language.Items.AddRange(["Türkçe", "English"]); Language.SelectedIndex = 0;
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, Padding = Options.Padding };
        root.ColumnStyles.Add(new(SizeType.Percent, 100));
        root.RowStyles.Add(new(SizeType.AutoSize)); root.RowStyles.Add(new(SizeType.Percent, 100));
        var host = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 4, Margin = new Padding(0, 0, 0, 12) };
        host.ColumnStyles.Add(new(SizeType.AutoSize)); host.ColumnStyles.Add(new(SizeType.Percent, 100)); host.ColumnStyles.Add(new(SizeType.AutoSize)); host.ColumnStyles.Add(new(SizeType.AutoSize));
        host.Controls.Add(HostLabel, 0, 0); host.Controls.Add(HostPath, 1, 0); host.Controls.Add(Browse, 2, 0); host.Controls.Add(Language, 3, 0); root.Controls.Add(host, 0, 0);
        var split = new SplitContainer { Size = Options.Window, Dock = DockStyle.Fill, SplitterDistance = 260, Panel1MinSize = 200, Panel2MinSize = 500 };
        split.Panel1.Controls.Add(Examples);
        var work = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 6, Padding = new Padding(12, 0, 0, 0) };
        work.ColumnStyles.Add(new(SizeType.Percent, 100));
        work.RowStyles.Add(new(SizeType.AutoSize)); work.RowStyles.Add(new(SizeType.Absolute, 70)); work.RowStyles.Add(new(SizeType.Absolute, 52)); work.RowStyles.Add(new(SizeType.AutoSize)); work.RowStyles.Add(new(SizeType.Percent, 100)); work.RowStyles.Add(new(SizeType.AutoSize));
        work.Controls.Add(Heading, 0, 0); work.Controls.Add(Instructions, 0, 1); work.Controls.Add(Expected, 0, 2);
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = true, Margin = new Padding(0, 8, 0, 8) };
        buttons.Controls.AddRange([Compose, Run, Stop, Copy, Save, Reset, Common, Cleanup]); work.Controls.Add(buttons, 0, 3);
        var editor = new SplitContainer { Size = Options.Window, Dock = DockStyle.Fill, Orientation = Orientation.Horizontal, SplitterDistance = 320 };
        var codePane = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2 };
        codePane.ColumnStyles.Add(new(SizeType.Percent, 100));
        codePane.RowStyles.Add(new(SizeType.AutoSize)); codePane.RowStyles.Add(new(SizeType.Percent, 100)); codePane.Controls.Add(CodeLabel, 0, 0); codePane.Controls.Add(Code, 0, 1);
        var outputPane = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2 };
        outputPane.ColumnStyles.Add(new(SizeType.Percent, 100));
        outputPane.RowStyles.Add(new(SizeType.AutoSize)); outputPane.RowStyles.Add(new(SizeType.Percent, 100));
        var outputHeading = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true }; outputHeading.Controls.AddRange([OutputLabel, ClearOutput]); outputPane.Controls.Add(outputHeading, 0, 0); outputPane.Controls.Add(Output, 0, 1);
        editor.Panel1.Controls.Add(codePane); editor.Panel2.Controls.Add(outputPane); work.Controls.Add(editor, 0, 4); work.Controls.Add(State, 0, 5); split.Panel2.Controls.Add(work); root.Controls.Add(split, 0, 1); Controls.Add(root);
        Run.BackColor = Options.Accent; Run.ForeColor = Color.White; Run.FlatStyle = FlatStyle.Flat;
        Browse.Click += (_, _) => { using var dialog = new OpenFileDialog { Filter = "UsefulNotification.exe|UsefulNotification.exe|Programs|*.exe", FileName = HostPath.Text }; if (dialog.ShowDialog(this) == DialogResult.OK) HostPath.Text = dialog.FileName; };
        Examples.BeforeSelect += (_, args) => { if (!Loading && args.Node?.Tag is Example example && Selected?.Id != example.Id && !ConfirmDiscard()) args.Cancel = true; };
        Examples.AfterSelect += (_, args) => { if (!Loading && args.Node?.Tag is Example example) SelectExample(example); };
        Language.SelectedIndexChanged += (_, _) => ApplyLanguage();
        Run.Click += async (_, _) => { Execution = RunExampleAsync(); await Execution; }; Stop.Click += async (_, _) => await StopAsync();
        Copy.Click += (_, _) => Guard(() => Clipboard.SetText(File.ReadAllText(Path.Combine(Options.Demo, "Runner.cs.template")).Replace("args[0]", JsonSerializer.Serialize(Path.GetFullPath(HostPath.Text))).Replace("args[1]", JsonSerializer.Serialize(Path.Combine(Options.Demo, "Assets"))).Replace("{{CODE}}", Code.Text))); Save.Click += (_, _) => Guard(SaveCode); Reset.Click += (_, _) => { if (Selected is not null && ConfirmDiscard()) LoadCode(Selected, false); };
        Common.Click += (_, _) => MessageBox.Show(this, "using UsefulNotification;\n\nNotificationClient.Options = new()\n{\n    Application = new FileInfo(@\"" + HostPath.Text + "\")\n};\n\n// token: cancellation token\n// assets: Demo/Assets directory\n// Print(value): safe result output", T("Ortak kurulum", "Shared setup"));
        Cleanup.Click += async (_, _) => await CleanupAsync(); ClearOutput.Click += (_, _) => Output.Clear();
        Compose.Click += (_, _) => new NotificationPicker(Options, HostPath.Text, English).Show(this);
        FormClosing += async (_, args) => { if (ClosingAfterStop) return; if (!ConfirmDiscard()) { args.Cancel = true; return; } if (Running) { args.Cancel = true; await StopAsync(); if (Execution is not null) await Execution; ClosingAfterStop = true; Close(); } };
        KeyPreview = true; KeyDown += async (_, args) => { if (args.KeyCode == Keys.F5 && !Running) { args.Handled = true; Execution = RunExampleAsync(); await Execution; } };
        ApplyLanguage();
        if (Selected is null) SelectExample(Catalog[0]);
        Shown += (_, _) => Examples.Focus();
    }

    /// <summary>Chooses real catalog text in the selected interface language.</summary>
    string T(string turkish, string english) => English ? english : turkish;

    /// <summary>Rebuilds navigation labels while retaining the selected case and edited code.</summary>
    void ApplyLanguage()
    {
        Compose.Text = T("Bildirim oluştur", "Compose notification");
        HostLabel.Text = T("Bildirim programı", "Notification program"); Browse.Text = T("Seç", "Browse"); Run.Text = T("Çalıştır · F5", "Run · F5"); Stop.Text = T("Durdur", "Stop"); Copy.Text = T("Tam kodu kopyala", "Copy complete code"); Save.Text = T("Kodu kaydet", "Save code"); Reset.Text = T("Özgün kod", "Original code"); Common.Text = T("Ortak kurulum", "Shared setup"); Cleanup.Text = T("Demo bildirimlerini sil", "Delete demo notifications"); ClearOutput.Text = T("Çıktıyı temizle", "Clear output"); CodeLabel.Text = T("Derlenip çalıştırılacak C# kodu (düzenlenebilir)", "C# code to compile and run (editable)"); OutputLabel.Text = T("Gerçek sonuç / yanıt / hata", "Actual result / reply / error");
        Loading = true; Examples.BeginUpdate(); Examples.Nodes.Clear();
        foreach (var group in Catalog.GroupBy(item => T(item.Category, item.CategoryEn)))
        {
            var category = Examples.Nodes.Add(group.Key);
            foreach (var example in group) { var node = category.Nodes.Add(T(example.Title, example.TitleEn)); node.Tag = example; if (Selected?.Id == example.Id) Examples.SelectedNode = node; }
        }
        Examples.EndUpdate(); Loading = false;
        if (Selected is null) { Examples.Nodes[0].Expand(); Examples.SelectedNode = Examples.Nodes[0].Nodes[0]; } else ShowDescription();
    }

    /// <summary>Prevents a case switch from silently dropping an edited sample.</summary>
    void SelectExample(Example example) { if (Selected?.Id == example.Id) return; Selected = example; LoadCode(example, true); ShowDescription(); }

    /// <summary>Shows what the user should do and the observable result, including audible examples.</summary>
    void ShowDescription()
    {
        if (Selected is null) return;
        Heading.Text = T(Selected.Title, Selected.TitleEn);
        Instructions.Text = T(Selected.Instructions, Selected.InstructionsEn) + (Selected.Audible ? T("  Bu örnek ses çalar.", "  This example plays sound.") : string.Empty);
        Expected.Text = T("Beklenen: ", "Expected: ") + T(Selected.Expected, Selected.ExpectedEn);
    }

    /// <summary>Loads a user override or the shipped source body.</summary>
    void LoadCode(Example example, bool useSaved)
    {
        string saved = Path.Combine(Options.UserCode, example.File);
        Code.Text = File.ReadAllText(useSaved && File.Exists(saved) ? saved : Path.Combine(Options.Demo, "Examples", example.File)); LoadedCode = Code.Text.ReplaceLineEndings("\n"); Code.Modified = false;
    }

    /// <summary>Asks only about actual unsaved code edits.</summary>
    bool ConfirmDiscard()
    {
        if (Code.Text.ReplaceLineEndings("\n") == LoadedCode) return true;
        var answer = MessageBox.Show(this, T("Kod değişiklikleri kaydedilsin mi?", "Save your code changes?"), Text, MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
        if (answer == DialogResult.Yes) { try { SaveCode(); } catch (Exception error) { Append(error.ToString()); return false; } }
        return answer != DialogResult.Cancel;
    }

    /// <summary>Persists code edits outside shipped program files so updates retain them.</summary>
    void SaveCode() { if (Selected is null) return; Directory.CreateDirectory(Options.UserCode); File.WriteAllText(Path.Combine(Options.UserCode, Selected.File), Code.Text); LoadedCode = Code.Text.ReplaceLineEndings("\n"); Code.Modified = false; State.Text = T("Kod kaydedildi.", "Code saved."); }

    /// <summary>Compiles the exact visible source through PowerShell/csc, then starts its own runner with captured output.</summary>
    async Task RunExampleAsync()
    {
        if (Running || Selected is null) return;
        Running = true; StopRequested = false; Run.Enabled = Save.Enabled = Reset.Enabled = Browse.Enabled = Cleanup.Enabled = Examples.Enabled = Language.Enabled = HostPath.Enabled = false; Stop.Enabled = true; Code.ReadOnly = true;
        try
        {
            if (!File.Exists(HostPath.Text)) throw new FileNotFoundException(T("UsefulNotification.exe dosyasını seç.", "Select UsefulNotification.exe."), HostPath.Text);
            string directory = Path.Combine(Options.Cache, "Runs", Selected.Id + "-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(directory);
            LogPath = Path.Combine(directory, "Output.log");
            string source = Path.Combine(directory, "VisibleExample.cs");
            File.WriteAllText(source, File.ReadAllText(Path.Combine(Options.Demo, "Runner.cs.template")).Replace("{{CODE}}", Code.Text));
            Append(T("Derleniyor: ", "Compiling: ") + Selected.Title); State.Text = T("csc derlemesi", "csc compilation");
            var compile = Start("pwsh"); compile.ArgumentList.Add("-NoProfile"); compile.ArgumentList.Add("-File"); compile.ArgumentList.Add(Path.Combine(Options.Demo, "Build.ps1")); compile.ArgumentList.Add("-ExampleSource"); compile.ArgumentList.Add(source); compile.ArgumentList.Add("-OutputDirectory"); compile.ArgumentList.Add(directory); compile.ArgumentList.Add("-PayloadDirectory"); compile.ArgumentList.Add(Options.Payload);
            IsExampleProcess = false;
            if (await RunProcessAsync(compile) != 0) { if (StopRequested) { State.Text = T("Durduruldu.", "Cancelled."); return; } throw new InvalidOperationException(T("Derleme başarısız; hata yukarıda.", "Compilation failed; see the error above.")); }
            if (StopRequested) { State.Text = T("Durduruldu.", "Cancelled."); return; }
            var run = Start("dotnet"); run.ArgumentList.Add(Path.Combine(directory, Options.ExampleAssembly)); run.ArgumentList.Add(Path.GetFullPath(HostPath.Text)); run.ArgumentList.Add(Path.Combine(Options.Demo, "Assets"));
            IsExampleProcess = true; State.Text = T("Çalışıyor; gerekiyorsa bildirimle etkileşime gir.", "Running; interact with the notification when requested.");
            int exit = await RunProcessAsync(run); State.Text = StopRequested || exit == 2 ? T("Durduruldu.", "Cancelled.") : exit == 0 ? T("Tamamlandı.", "Completed.") : T("Hata; ayrıntılar çıktıda.", "Failed; see output.");
        }
        catch (Exception error) { Append(error.ToString()); State.Text = T("Hata oluştu.", "Failed."); }
        finally { CurrentProcess = null; Running = false; Run.Enabled = Save.Enabled = Reset.Enabled = Browse.Enabled = Cleanup.Enabled = Examples.Enabled = Language.Enabled = HostPath.Enabled = true; Stop.Enabled = false; Code.ReadOnly = false; }
    }

    /// <summary>Creates a hidden redirected child process with intact argument boundaries.</summary>
    ProcessStartInfo Start(string executable) => new(executable) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true, WorkingDirectory = Options.Root };

    /// <summary>Awaits both output streams and the owned process rather than leaving detached work.</summary>
    async Task<int> RunProcessAsync(ProcessStartInfo start)
    {
        using var process = Process.Start(start) ?? throw new IOException("The demo process did not start."); CurrentProcess = process;
        async Task Read(StreamReader reader) { while (await reader.ReadLineAsync() is { } line) { try { using var json = JsonDocument.Parse(line); Append(JsonSerializer.Serialize(json.RootElement, new JsonSerializerOptions { WriteIndented = true })); } catch (JsonException) { Append(line); } } }
        await Task.WhenAll(Read(process.StandardOutput), Read(process.StandardError), process.WaitForExitAsync());
        CurrentProcess = null; return process.ExitCode;
    }

    /// <summary>Cancels a cooperative runner through stdin; an unresponsive owned process is terminated after the stop grace period.</summary>
    async Task StopAsync()
    {
        try
        {
            if (CurrentProcess is not { } process || process.HasExited) return;
            StopRequested = true;
            Stop.Enabled = false;
            if (IsExampleProcess) { var exited = process.WaitForExitAsync(); await process.StandardInput.WriteLineAsync(); if (await Task.WhenAny(exited, Task.Delay(Options.StopGrace)) != exited && !exited.IsCompleted) process.Kill(true); }
            else process.Kill(true);
        }
        catch (Exception error) { Append(error.ToString()); }
    }

    /// <summary>Removes only series whose sender is this demo runner, leaving other programs' notifications untouched.</summary>
    async Task CleanupAsync()
    {
        Cleanup.Enabled = false;
        try
        {
            var options = new NotificationClientOptions { Application = new FileInfo(HostPath.Text) };
            var list = await NotificationClient.ExecuteAsync(new() { Operation = NotificationOperation.List }, options);
            if (!list.Accepted) throw new IOException(list.Failure?.Message);
            int count = 0;
            foreach (var item in list.Entries?.Where(item => item.Request.Source == Path.GetFileNameWithoutExtension(Options.ExampleAssembly)) ?? []) { var result = await NotificationClient.ExecuteAsync(new() { Operation = NotificationOperation.Delete, Id = item.Id }, options); if (result.Accepted) count++; }
            Append(T("Silinen demo bildirimi: ", "Demo notifications deleted: ") + count);
        }
        catch (Exception error) { Append(error.ToString()); }
        finally { Cleanup.Enabled = true; }
    }

    /// <summary>Shows and retains the actual compiler/process result; no test outcome is synthesized.</summary>
    void Append(string text)
    {
        if (IsDisposed) return;
        Output.AppendText(text + Environment.NewLine);
        if (LogPath is null) return;
        try { File.AppendAllText(LogPath, text + Environment.NewLine); }
        catch (Exception error) { LogPath = null; Output.AppendText(error.ToString() + Environment.NewLine); }
    }

    /// <summary>Contains the final failure of a small synchronous UI action.</summary>
    void Guard(Action action) { try { action(); } catch (Exception error) { Append(error.ToString()); } }
}

/// <summary>Combines actual public presets and sends their text-boundary representation to notify.exe without compiling an example.</summary>
internal sealed class NotificationPicker : Form
{
    /// <summary>Retains the selected value directly on its radio button.</summary>
    sealed record Choice<T>(string Label, T Value);
    /// <summary>Combines an optional sound definition with explicit sound suppression.</summary>
    sealed record SoundChoice(SoundPattern? Pattern = null, bool Silent = false);
    readonly bool English;
    readonly string CommandPath;
    readonly string Source;
    readonly TextBox TitleInput = new() { Dock = DockStyle.Fill };
    readonly TextBox MessageInput = new() { Dock = DockStyle.Fill };
    readonly TextBox Preview = new() { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical };
    readonly TextBox Result = new() { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical };
    readonly Button Send = new() { AutoSize = true };
    readonly Func<NotificationTheme?> Theme;
    readonly Func<NotificationLevel?> Level;
    readonly Func<NotificationPriority> Priority;
    readonly Func<SoundChoice> Sound;
    readonly Func<NotificationLifetime?> Lifetime;
    bool Sending;

    /// <summary>Builds independent radio groups; opening this window never sends or compiles anything.</summary>
    /// <param name="options">Existing demo dimensions and appearance.</param>
    /// <param name="host">Selected portable launcher; notify.exe is resolved beside it.</param>
    /// <param name="english">Whether captions should use English.</param>
    public NotificationPicker(DemoOptions options, string host, bool english)
    {
        English = english; Text = T("Bildirim oluştur", "Compose notification"); Font = options.TextFont;
        Size = new(980, 850); MinimumSize = new(820, 650); StartPosition = FormStartPosition.CenterParent;
        CommandPath = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(host))!, "notify.exe");
        Source = Path.GetFileNameWithoutExtension(options.ExampleAssembly);
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = options.Padding, RowCount = 4 };
        root.ColumnStyles.Add(new(SizeType.Percent, 100));
        root.RowStyles.Add(new(SizeType.Percent, 65)); root.RowStyles.Add(new(SizeType.AutoSize));
        root.RowStyles.Add(new(SizeType.Percent, 20)); root.RowStyles.Add(new(SizeType.Percent, 15));
        var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
        var choices = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 1 };
        choices.ColumnStyles.Add(new(SizeType.Percent, 100)); scroll.Controls.Add(choices);
        var content = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2 };
        content.ColumnStyles.Add(new(SizeType.AutoSize)); content.ColumnStyles.Add(new(SizeType.Percent, 100));
        content.Controls.Add(new Label { Text = T("Başlık", "Title"), AutoSize = true }, 0, 0); content.Controls.Add(TitleInput, 1, 0);
        content.Controls.Add(new Label { Text = T("Mesaj", "Message"), AutoSize = true }, 0, 1); content.Controls.Add(MessageInput, 1, 1);
        TitleInput.Text = T("Deneme", "Preview"); MessageInput.Text = T("Bildirim içeriği", "Notification content"); choices.Controls.Add(content);
        Theme = Group(choices, T("Tema · görünüm", "Theme · appearance"), new[] { new Choice<NotificationTheme?>(T("Panelde seçili", "Panel selection"), null) }.Concat(NotificationThemes.All.Select(theme => new Choice<NotificationTheme?>(theme.Name, theme))));
        Level = Group(choices, T("Seviye · vurgulu kenarlık rengi", "Level · accent border color"), new[] { new Choice<NotificationLevel?>(T("Temanın varsayılanı", "Theme default"), null) }.Concat(Enum.GetValues<NotificationLevel>().Select(level => new Choice<NotificationLevel?>(level.ToString(), level))));
        Priority = Group(choices, T("Öncelik · kuyruk sırası", "Priority · queue order"), new[] { NotificationPriority.Normal, NotificationPriority.Low, NotificationPriority.High, NotificationPriority.Urgent }.Select(priority => new Choice<NotificationPriority>(priority.ToString(), priority)));
        Sound = Group(choices, T("Ses", "Sound"), new[] { new Choice<SoundChoice>(T("Panelde seçili", "Panel selection"), new()), new Choice<SoundChoice>(T("Sessiz", "Silent"), new(Silent: true)) }.Concat(NotificationSounds.All.Select(sound => new Choice<SoundChoice>(sound.Name, new(sound)))));
        Lifetime = Group(choices, T("Gösterim süresi", "Display lifetime"), new[] { new Choice<NotificationLifetime?>(T("Varsayılan", "Default"), null), new Choice<NotificationLifetime?>(T("8 saniye", "8 seconds"), NotificationLifetimes.Default), new Choice<NotificationLifetime?>(T("Kapatana kadar", "Until dismissed"), NotificationLifetimes.UntilAcknowledged) });
        Send.Text = T("Bildirim yolla", "Send notification"); Send.BackColor = options.Accent; Send.ForeColor = Color.White;
        root.Controls.Add(scroll, 0, 0); root.Controls.Add(Send, 0, 1); root.Controls.Add(Preview, 0, 2); root.Controls.Add(Result, 0, 3); Controls.Add(root);
        Send.Click += async (_, _) => await SendAsync();
        TitleInput.TextChanged += (_, _) => RefreshPreview(); MessageInput.TextChanged += (_, _) => RefreshPreview();
        foreach (var radio in choices.Controls.OfType<GroupBox>().SelectMany(group => group.Controls.OfType<FlowLayoutPanel>()).SelectMany(flow => flow.Controls.OfType<RadioButton>())) radio.CheckedChanged += (_, _) => RefreshPreview();
        FormClosing += (_, args) => { if (Sending) args.Cancel = true; };
        RefreshPreview();
    }

    /// <summary>Chooses the requested interface language.</summary>
    string T(string turkish, string english) => English ? english : turkish;

    /// <summary>Creates one mutually exclusive group and returns its actual selected object.</summary>
    /// <typeparam name="T">The preset or finite setting retained by the controls.</typeparam>
    /// <param name="parent">Scrollable settings layout.</param>
    /// <param name="caption">Visible group heading.</param>
    /// <param name="choices">Labeled values; the first value is initially selected.</param>
    /// <returns>A reader of the selected value without name-to-object lookup.</returns>
    static Func<T> Group<T>(TableLayoutPanel parent, string caption, IEnumerable<Choice<T>> choices)
    {
        var group = new GroupBox { Text = caption, Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(8, 22, 8, 8), Margin = new Padding(0, 6, 0, 6) };
        var flow = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, WrapContents = true };
        foreach (var choice in choices) flow.Controls.Add(new RadioButton { Text = choice.Label, Tag = choice, AutoSize = true, Checked = flow.Controls.Count == 0, Margin = new Padding(6) });
        group.Controls.Add(flow); parent.Controls.Add(group);
        return () => ((Choice<T>)flow.Controls.OfType<RadioButton>().Single(radio => radio.Checked).Tag!).Value;
    }

    /// <summary>Projects only supported text-boundary fields, preserving omission for panel defaults.</summary>
    /// <returns>The JSON body supplied verbatim to notify.exe standard input.</returns>
    string Request()
    {
        var request = new Dictionary<string, object?> { ["title"] = TitleInput.Text, ["message"] = MessageInput.Text, ["source"] = Source, ["priority"] = Priority().ToString() };
        if (Theme() is { } theme) request["theme"] = theme.Name;
        if (Level() is { } level) request["level"] = level.ToString();
        var sound = Sound();
        if (sound.Silent) request["silent"] = true;
        if (sound.Pattern is { } pattern) request["sound"] = new { steps = pattern.Steps.Select(step => new { frequency = step.Frequency, duration = step.Duration.ToString(), pause = step.Pause.ToString() }) };
        if (Lifetime() is AcknowledgedLifetime) request["lifetime"] = new { mode = "acknowledge" };
        else if (Lifetime() is ExpiringLifetime expiry) request["lifetime"] = new { mode = "expire", duration = expiry.Duration.ToString() };
        return JsonSerializer.Serialize(request, new JsonSerializerOptions { WriteIndented = true });
    }

    /// <summary>Displays the exact executable and body that will be sent.</summary>
    void RefreshPreview() => Preview.Text = "\"" + CommandPath + "\" --stdin" + Environment.NewLine + Request();

    /// <summary>Sends directly to the existing command executable, awaits its actual response and never invokes a compiler.</summary>
    async Task SendAsync()
    {
        if (Sending) return;
        Sending = true; Send.Enabled = false;
        try
        {
            if (!File.Exists(CommandPath)) throw new FileNotFoundException("notify.exe was not found beside the selected host.", CommandPath);
            string request = Request(); Preview.Text = "\"" + CommandPath + "\" --stdin" + Environment.NewLine + request;
            var start = new ProcessStartInfo(CommandPath) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true };
            start.ArgumentList.Add("--stdin");
            using var process = Process.Start(start) ?? throw new IOException("The notification sender did not start.");
            Task<string> output = process.StandardOutput.ReadToEndAsync(), error = process.StandardError.ReadToEndAsync();
            await process.StandardInput.WriteAsync(request); process.StandardInput.Close();
            await process.WaitForExitAsync();
            Result.Text = (await output) + (await error);
            if (process.ExitCode != 0) Result.AppendText(Environment.NewLine + T("Gönderim başarısız. Çıkış kodu: ", "Send failed. Exit code: ") + process.ExitCode);
        }
        catch (Exception error) { Result.Text = error.ToString(); }
        finally { Sending = false; Send.Enabled = true; }
    }
}
