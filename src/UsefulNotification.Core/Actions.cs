using System.Text.Json.Serialization;
namespace UsefulNotification;

/// <summary>Describes an actual user-selected action, with its required data and behavior together.</summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(DismissAction), "dismiss")]
[JsonDerivedType(typeof(ReplyAction), "reply")]
[JsonDerivedType(typeof(CallbackAction), "callback")]
[JsonDerivedType(typeof(SnoozeAction), "snooze")]
[JsonDerivedType(typeof(OpenUriAction), "openUri")]
[JsonDerivedType(typeof(RunCommandAction), "runCommand")]
public abstract record NotificationAction
{
    /// <summary>Declared action identity, initially a new GUID string. Results return this value; identities must be unique within a notification.</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();
    /// <summary>Optional button caption; null uses the localized default for this action type.</summary>
    public string? Label { get; set; }
    [JsonIgnore] internal virtual bool External => false;
    [JsonIgnore] internal virtual string DefaultLabel => Localization.Strings.Dismiss;
    internal virtual void Validate() { }
    internal virtual Task ExecuteAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
/// <summary>Acknowledges a notification.</summary>
public sealed record DismissAction : NotificationAction;
/// <summary>Submits the reply and selected checklist items.</summary>
public sealed record ReplyAction : NotificationAction
{
    internal override string DefaultLabel => Localization.Strings.Reply;
}
/// <summary>Returns the selected action without starting an external program.</summary>
public sealed record CallbackAction : NotificationAction;
/// <summary>Defers the next display by a positive duration.</summary>
/// <param name="Delay">Positive duration from the accepted interaction to the next occurrence.</param>
public sealed record SnoozeAction(TimeSpan Delay) : NotificationAction
{
    internal override string DefaultLabel => Localization.Strings.Snooze;
    internal override void Validate() { if (Delay <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(Delay)); }
}
/// <summary>Opens an absolute HTTP, HTTPS or mail link after the interaction has been committed.</summary>
/// <param name="Target">Absolute HTTP, HTTPS or mailto URI opened by the host through the registered Windows handler.</param>
public sealed record OpenUriAction(Uri Target) : NotificationAction
{
    internal override bool External => true;
    internal override void Validate()
    {
        if (!Target.IsAbsoluteUri || !AppConstants.Actions.LinkSchemes.Contains(Target.Scheme)) throw new ArgumentException("Unsupported action URI.");
    }
    internal override Task ExecuteAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var process = Process.Start(new ProcessStartInfo(Target.AbsoluteUri) { UseShellExecute = true });
        return Task.CompletedTask;
    }
}
/// <summary>Starts a configured command. PATH names and ArgumentList are preserved; credentials and redirected streams are not transferred.</summary>
/// <param name="StartInfo">Launch options captured with the request. FileName, arguments, working directory, shell choice, window style and CreateNoWindow are transferred; unsupported process features are rejected.</param>
public sealed record RunCommandAction(ProcessStartInfo StartInfo) : NotificationAction
{
    internal override bool External => true;
    internal override void Validate() => ProcessStartConverter.Validate(StartInfo);
    internal override Task ExecuteAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var process = Process.Start(StartInfo) ?? throw new IOException("The command did not start.");
        return Task.CompletedTask;
    }
}
