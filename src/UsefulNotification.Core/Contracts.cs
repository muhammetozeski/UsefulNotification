using System.Text.Json.Serialization;
namespace UsefulNotification;

/// <summary>Classifies meaning and accent.</summary>
public enum NotificationLevel
{
    /// <summary>Neutral content using the palette's normal accent.</summary>
    Normal,
    /// <summary>Informational content; the default classification for a plain send.</summary>
    Info,
    /// <summary>Successful completion or another positive result.</summary>
    Success,
    /// <summary>A condition needing attention without implying operation failure.</summary>
    Warning,
    /// <summary>A failed operation or error condition.</summary>
    Error,
    /// <summary>Time-sensitive attention content; queue priority is selected separately.</summary>
    Urgent,
    /// <summary>Content associated with a due date or deadline.</summary>
    Deadline,
    /// <summary>Meeting-related content.</summary>
    Meeting,
    /// <summary>Health-related reminder content.</summary>
    Health,
    /// <summary>A rest or break reminder.</summary>
    Break,
    /// <summary>Personal content using its own palette accent.</summary>
    Personal
}
/// <summary>Orders the notification queue.</summary>
public enum NotificationPriority
{
    /// <summary>Waits behind normal and higher-priority eligible notifications.</summary>
    Low,
    /// <summary>Default queue precedence.</summary>
    Normal,
    /// <summary>Precedes normal and low-priority eligible notifications.</summary>
    High,
    /// <summary>Highest queue precedence; does not interrupt an already assigned renderer slot.</summary>
    Urgent
}
/// <summary>Describes a stored display occurrence.</summary>
public enum NotificationStatus
{
    /// <summary>Waiting for a future initial or recurring due time.</summary>
    Scheduled,
    /// <summary>Eligible for delivery or assigned to rendering, with no confirmed visible display yet.</summary>
    Pending,
    /// <summary>Matching HTML content has confirmed visible delivery.</summary>
    Visible,
    /// <summary>The occurrence was deferred, or its new deferred occurrence is waiting for its due time.</summary>
    Snoozed,
    /// <summary>Completed by acknowledgement or a successful selected action.</summary>
    Dismissed,
    /// <summary>Completed after its confirmed-display lifetime elapsed.</summary>
    Expired,
    /// <summary>Completed after delivery failure or an unsuccessful external action.</summary>
    Failed
}
/// <summary>Identifies a local management operation.</summary>
public enum NotificationOperation
{
    /// <summary>Accepts a complete request; repeated sends with its stable identity acknowledge the existing acceptance.</summary>
    Send,
    /// <summary>Merges only explicitly supplied NotificationUpdate fields.</summary>
    Update,
    /// <summary>Completes the targeted active occurrence by acknowledgement.</summary>
    Dismiss,
    /// <summary>Completes the active occurrence and creates a deferred occurrence.</summary>
    Snooze,
    /// <summary>Returns retained notification series.</summary>
    List,
    /// <summary>Removes completed series while retaining active and future notifications.</summary>
    Clear,
    /// <summary>Requests opening the tray panel.</summary>
    ShowPanel,
    /// <summary>Reloads user settings and file-backed themes.</summary>
    ReloadSettings,
    /// <summary>Requests an acknowledged host shutdown.</summary>
    Shutdown,
    /// <summary>Returns the retained series selected by its identity.</summary>
    Get,
    /// <summary>Removes the selected retained series.</summary>
    Delete,
    /// <summary>Changes manual ordering for the selected series.</summary>
    Move,
    /// <summary>Replaces a complete request, creating a new occurrence when rescheduled or previously completed.</summary>
    Replace,
    /// <summary>Closes the current occurrence and immediately retries the retained content with a new occurrence identity, preserving the series anchor.</summary>
    Retry,
    /// <summary>Removes completed occurrences while preserving active and future displays. Id and OccurrenceId optionally restrict the removal.</summary>
    ClearHistory
}
/// <summary>Classifies a rejection without parsing message text.</summary>
public enum NotificationError
{
    /// <summary>The command or request violates the accepted contract.</summary>
    InvalidRequest,
    /// <summary>The targeted retained notification could not be found.</summary>
    NotFound,
    /// <summary>The host could not persist the requested change.</summary>
    StorageFailure,
    /// <summary>The requested display could not be confirmed.</summary>
    DeliveryFailure,
    /// <summary>The contacted host uses an incompatible wire contract.</summary>
    IncompatibleHost,
    /// <summary>The operation failed outside the more specific rejection categories.</summary>
    OperationFailed
}
/// <summary>Describes an external action attempt.</summary>
public enum ActionStatus
{
    /// <summary>The interaction is persisted and its external effect awaits a recorded outcome.</summary>
    Pending,
    /// <summary>The external launch completed successfully; this does not await the launched program's exit.</summary>
    Succeeded,
    /// <summary>The external effect threw and its failure was recorded.</summary>
    Failed,
    /// <summary>Host shutdown or restart prevented a conclusive recorded outcome; the effect is not replayed.</summary>
    Interrupted
}
internal enum NotificationKind { Text, Reminder, Progress, Image, Choice, Input, Checklist, Custom }

/// <summary>All fields are optional. Send captures an independent snapshot before awaiting a connection.</summary>
public sealed record NotificationRequest
{
    /// <summary>Optional stable series identity. Send assigns a new identity when omitted; resending that identity returns its accepted occurrence.</summary>
    public Guid? Id { get; set; }
    /// <summary>Sender display name. Send uses the entry assembly name when null; a blank name falls back to the host name.</summary>
    public string? Source { get; set; }
    /// <summary>Card heading. A new send replaces null or whitespace with the resolved sender name.</summary>
    public string? Title { get; set; }
    /// <summary>Card body. A new send supplies localized default content for null or whitespace.</summary>
    public string? Message { get; set; }
    /// <summary>Semantic accent classification. Null resolves the theme default, then the host default.</summary>
    public NotificationLevel? Level { get; set; }
    /// <summary>Queue precedence. Null becomes Normal when a new request is accepted.</summary>
    public NotificationPriority? Priority { get; set; }
    /// <summary>Optional sender-defined grouping text retained in the journal.</summary>
    public string? Group { get; set; }
    /// <summary>Optional sender-scoped replacement key; an existing notification with the same source and tag is reused.</summary>
    public string? Tag { get; set; }
    /// <summary>Complete appearance, such as NotificationThemes.Happy; null uses the host selection.</summary>
    public NotificationTheme? Theme { get; set; }
    /// <summary>Optional complete replacements for selected theme sections; null retains the selected theme unchanged.</summary>
    public NotificationAppearance? Appearance { get; set; }
    /// <summary>Explicit lifetime; null chooses expiry or acknowledgement from the actual content.</summary>
    public NotificationLifetime? Lifetime { get; set; }
    /// <summary>True suppresses the display sound; null follows the host sound preference.</summary>
    public bool? Silent { get; set; }
    /// <summary>Optional initial delivery and recurrence. Null makes the initial occurrence eligible immediately.</summary>
    public ScheduleOptions? Schedule { get; set; }
    /// <summary>Complete sound sequence or file, such as NotificationSounds.Subtle. Null uses the selected host sound; Silent can suppress either.</summary>
    public SoundPattern? Sound { get; set; }
    /// <summary>Optional positive snooze durations. Null uses the host choices; values are durations rather than minute counts.</summary>
    public TimeSpan[]? Snooze { get; set; }
    /// <summary>Declared action objects displayed as buttons. Defaults to an empty list; accepted requests own a serialized copy.</summary>
    public List<NotificationAction> Actions { get; set; } = [];
    /// <summary>Optional progress content. Incomplete progress normally remains until acknowledgement.</summary>
    public ProgressContent? Progress { get; set; }
    /// <summary>Optional absolute image URI or local file reference. The host loads the image for the card.</summary>
    public NotificationImage? Image { get; set; }
    /// <summary>Optional reply field; submitted text is returned in the occurrence result.</summary>
    public InputContent? Input { get; set; }
    /// <summary>Selectable items, initially empty. Results return selected item identities rather than labels.</summary>
    public List<ChecklistItem> Checklist { get; set; } = [];
    /// <summary>Optional complete HTML card replacement. Null uses the standard renderer.</summary>
    public HtmlTemplateContent? Template { get; set; }
    /// <summary>Sender-defined template values, initially empty. The template renderer substitutes matching placeholders.</summary>
    public Dictionary<string, string> Data { get; set; } = [];
    /// <summary>Optional existing named pipe receiving one JSON result after completion is persisted. The host makes a bounded best-effort connection.</summary>
    public string? CallbackPipe { get; set; }
    [JsonIgnore] internal NotificationKind Kind => Template is not null ? NotificationKind.Custom : Progress is not null ? NotificationKind.Progress : Image is not null ? NotificationKind.Image : Input is not null ? NotificationKind.Input : Checklist.Count > 0 ? NotificationKind.Checklist : Actions.Count > 0 ? NotificationKind.Choice : Schedule is not null ? NotificationKind.Reminder : NotificationKind.Text;
}
/// <summary>Owns expiry independently of scheduling.</summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "mode")]
[JsonDerivedType(typeof(ExpiringLifetime), "expire")]
[JsonDerivedType(typeof(AcknowledgedLifetime), "acknowledge")]
public abstract record NotificationLifetime;
/// <summary>Keeps a notification until acknowledgement.</summary>
public sealed record AcknowledgedLifetime : NotificationLifetime;
/// <summary>Expires after a positive duration of confirmed display.</summary>
/// <param name="Duration">Positive lifetime as a duration, starting at confirmed visibility and excluding tracked pauses.</param>
public sealed record ExpiringLifetime(TimeSpan Duration) : NotificationLifetime;
/// <summary>Provides actual lifetime policy objects.</summary>
public static class NotificationLifetimes
{
    /// <summary>Reusable acknowledgement policy. Assign this object to Lifetime to keep the occurrence until an interaction completes it.</summary>
    public static AcknowledgedLifetime UntilAcknowledged = new();
    /// <summary>Reusable expiry policy, initially eight seconds of confirmed display. Send snapshots the current object value.</summary>
    public static ExpiringLifetime Default = new(TimeSpan.FromSeconds(8));
}
/// <summary>Specifies the first display and an optional recurrence object.</summary>
public sealed record ScheduleOptions
{
    /// <summary>Absolute first due time including its UTC offset. Mutually exclusive with After; null permits a relative delay.</summary>
    public DateTimeOffset? At { get; set; }
    /// <summary>Nonnegative delay from acceptance to the first due time. Mutually exclusive with At; both null mean immediate eligibility.</summary>
    public TimeSpan? After { get; set; }
    /// <summary>Optional recurrence object calculating later due times from the series anchor; null completes the series after its last occurrence.</summary>
    public NotificationRecurrence? Recurrence { get; set; }
}
/// <summary>References an image with a real URI or local file.</summary>
public sealed record NotificationImage
{
    /// <summary>Absolute image URI. A file URI references a host-accessible local file; construction does not read the image.</summary>
    public Uri Source { get; set; }
    /// <summary>References an image by its absolute URI without loading it in the sending process.</summary>
    /// <param name="source">Absolute remote or file URI that the host renderer can access.</param>
    [JsonConstructor] public NotificationImage(Uri source) => Source = source;
    /// <summary>References a local image using its absolute file URI.</summary>
    /// <param name="file">Local image path accessible to the host; construction does not read the file.</param>
    public NotificationImage(FileInfo file) : this(new Uri(file.FullName)) { }
}
/// <summary>Replaces the entire HTML card; null File selects the editable default.</summary>
/// <param name="File">Host-accessible HTML file, or null for the editable UserData/Templates/Card.html.</param>
public sealed record HtmlTemplateContent(FileInfo? File = null);
/// <summary>Stores determinate or indeterminate progress.</summary>
public sealed record ProgressContent
{
    /// <summary>Current progress units. Null selects indeterminate progress; a finite number is compared with Maximum.</summary>
    public double? Value { get; set; }
    /// <summary>Positive finite upper bound in the same units as Value; initially 100.</summary>
    public double Maximum { get; set; } = 100;
    /// <summary>Optional progress caption. Null lets the standard card show its numeric progress text.</summary>
    public string? Label { get; set; }
}
/// <summary>Configures a reply field.</summary>
public sealed record InputContent
{
    /// <summary>Optional hint shown while the reply field is empty.</summary>
    public string? Placeholder { get; set; }
    /// <summary>Optional initial reply text, copied into the card input when that occurrence is created.</summary>
    public string? Value { get; set; }
    /// <summary>True uses a text area; false, the default, uses a single-line field.</summary>
    public bool Multiline { get; set; }
    /// <summary>True rejects submission of an empty or whitespace reply; initially false.</summary>
    public bool Required { get; set; }
}
/// <summary>Identifies one selectable item.</summary>
public sealed record ChecklistItem
{
    /// <summary>Result identity, initially a new GUID string. Keep it stable when editing a checklist item.</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();
    /// <summary>Visible item label, initially empty. The result carries Id instead of this text.</summary>
    public string Text { get; set; } = string.Empty;
    /// <summary>Initial selection state, initially false.</summary>
    public bool Checked { get; set; }
}
/// <summary>Contains a complete tone sequence or local audio file.</summary>
public sealed record SoundPattern
{
    /// <summary>Preset identity, initially a new GUID; used to preserve selection independently of its display name.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();
    /// <summary>Editable display name, initially empty; C# callers pass the sound object directly.</summary>
    public string Name { get; set; } = string.Empty;
    /// <summary>Ordered tones and their following pauses, initially empty. Used when AudioFile is null.</summary>
    public List<ToneStep> Steps { get; set; } = [];
    /// <summary>Optional host-accessible local audio file. When supplied, playback uses the file instead of Steps.</summary>
    public FileInfo? AudioFile { get; set; }
}
/// <summary>Combines hertz, audible duration and following silence.</summary>
/// <param name="Frequency">Tone frequency in hertz, supported from 37 to 32767.</param>
/// <param name="Duration">Nonnegative duration during which the tone sounds.</param>
/// <param name="Pause">Nonnegative silence after the tone, before the next step.</param>
public sealed record ToneStep(int Frequency, TimeSpan Duration, TimeSpan Pause);
/// <summary>An absent change preserves a field; a supplied null value clears it.</summary>
/// <typeparam name="T">The actual value type of the field being updated, including nullability where clearing is supported.</typeparam>
/// <param name="Value">Replacement value. The containing update member being null means preserve; this value being null means clear.</param>
public sealed record Change<T>(T Value);
/// <summary>Updates supplied progress fields without resetting the others.</summary>
public sealed record ProgressUpdate
{
    /// <summary>Absent preserves the current value; Change with null selects indeterminate progress.</summary>
    public Change<double?>? Value { get; set; }
    /// <summary>Absent preserves the current maximum; a supplied value must be positive and finite.</summary>
    public Change<double>? Maximum { get; set; }
    /// <summary>Absent preserves the label; Change with null removes it.</summary>
    public Change<string?>? Label { get; set; }
}
/// <summary>Describes partial changes; empty supplied collections clear their contents.</summary>
public sealed record NotificationUpdate
{
    /// <summary>Absent preserves the sender; a supplied change replaces or clears the stored sender text.</summary>
    public Change<string?>? Source { get; set; }
    /// <summary>Absent preserves the heading; Change with null clears it without applying send defaults.</summary>
    public Change<string?>? Title { get; set; }
    /// <summary>Absent preserves the body; Change with null clears it without applying send defaults.</summary>
    public Change<string?>? Message { get; set; }
    /// <summary>Absent preserves the level; Change with null returns accent selection to the effective theme.</summary>
    public Change<NotificationLevel?>? Level { get; set; }
    /// <summary>Absent preserves queue precedence; a supplied change replaces or clears it.</summary>
    public Change<NotificationPriority?>? Priority { get; set; }
    /// <summary>Absent preserves grouping text; Change with null removes it.</summary>
    public Change<string?>? Group { get; set; }
    /// <summary>Absent preserves the replacement key; Change with null removes it.</summary>
    public Change<string?>? Tag { get; set; }
    /// <summary>Absent preserves the complete theme object; Change with null selects the host theme.</summary>
    public Change<NotificationTheme?>? Theme { get; set; }
    /// <summary>Absent preserves appearance overrides; Change with null removes them.</summary>
    public Change<NotificationAppearance?>? Appearance { get; set; }
    /// <summary>Absent preserves lifetime policy; Change with null restores content-based lifetime selection.</summary>
    public Change<NotificationLifetime?>? Lifetime { get; set; }
    /// <summary>Absent preserves sound suppression; Change with null returns to host preference.</summary>
    public Change<bool?>? Silent { get; set; }
    /// <summary>Absent preserves timing; Change with null removes the schedule and starts an immediate replacement occurrence.</summary>
    public Change<ScheduleOptions?>? Schedule { get; set; }
    /// <summary>Absent preserves the requested sound; Change with null returns to the selected host sound.</summary>
    public Change<SoundPattern?>? Sound { get; set; }
    /// <summary>Absent preserves snooze choices; Change with null returns to host choices.</summary>
    public Change<TimeSpan[]?>? Snooze { get; set; }
    /// <summary>Absent preserves buttons; Change with an empty list removes all declared actions.</summary>
    public Change<List<NotificationAction>>? Actions { get; set; }
    /// <summary>Merges only supplied progress fields, retaining the other progress values. Applied after ProgressContent when both are supplied.</summary>
    public ProgressUpdate? Progress { get; set; }
    /// <summary>Replaces the complete progress object; Change with null removes progress before the optional Progress merge.</summary>
    public Change<ProgressContent?>? ProgressContent { get; set; }
    /// <summary>Absent preserves the image; Change with null removes it.</summary>
    public Change<NotificationImage?>? Image { get; set; }
    /// <summary>Absent preserves reply configuration; Change with null removes the input.</summary>
    public Change<InputContent?>? Input { get; set; }
    /// <summary>Absent preserves checklist items; Change with an empty list removes all items.</summary>
    public Change<List<ChecklistItem>>? Checklist { get; set; }
    /// <summary>Absent preserves the custom card; Change with null restores the standard renderer.</summary>
    public Change<HtmlTemplateContent?>? Template { get; set; }
    /// <summary>Absent preserves template values; Change with an empty dictionary removes all values.</summary>
    public Change<Dictionary<string, string>>? Data { get; set; }
    /// <summary>Absent preserves the result destination; Change with null disables callbacks for later completions.</summary>
    public Change<string?>? CallbackPipe { get; set; }
}
/// <summary>Owns a series and its bounded display history.</summary>
public sealed record NotificationEntry
{
    /// <summary>Retained send identities and original accepted displays, including source/tag upserts. Bounded by retained displays and the host history capacity.</summary>
    public List<NotificationAcceptance> Acceptances { get; set; } = [];
    /// <summary>Stable notification series identity used by management commands.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();
    /// <summary>Accepted independent request snapshot. Editing a returned copy does not update the host; use UpdateAsync or Replace.</summary>
    public NotificationRequest Request { get; set; } = new();
    /// <summary>Acceptance timestamp, including the local UTC offset.</summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;
    /// <summary>Original due time used to calculate anchored recurrences; explicit rescheduling establishes a new anchor.</summary>
    public DateTimeOffset SeriesAnchor { get; set; } = DateTimeOffset.Now;
    /// <summary>Occurrence accepted by the original send, retained for idempotent resend acknowledgement.</summary>
    public Guid? AcceptedOccurrenceId { get; set; }
    /// <summary>Oldest-to-newest retained display history. The host bounds this list; old results can eventually be removed.</summary>
    public List<NotificationOccurrence> Occurrences { get; set; } = [new()];
    /// <summary>Manual queue ordering key; smaller values precede larger values within the same priority.</summary>
    public int Order { get; set; }
    /// <summary>Latest occurrence, including a future recurrence created after the preceding result.</summary>
    [JsonIgnore] public NotificationOccurrence Current => Occurrences[^1];
    /// <summary>Due time of Current; delivery still depends on queue capacity.</summary>
    [JsonIgnore] public DateTimeOffset DueAt { get => Current.DueAt; internal set => Current.DueAt = value; }
    /// <summary>Lifecycle status of Current rather than the most recently completed occurrence.</summary>
    [JsonIgnore] public NotificationStatus Status { get => Current.Status; internal set => Current.Status = value; }
    /// <summary>First confirmed visible timestamp of Current, or null before delivery.</summary>
    [JsonIgnore] public DateTimeOffset? VisibleAt => Current.VisibleAt;
    /// <summary>Current expiry deadline, or null for acknowledgement-based content or an unstarted lifetime.</summary>
    [JsonIgnore] public DateTimeOffset? ExpiresAt => Current.ExpiresAt;
    /// <summary>Timestamp when Current reserved a renderer slot, or null while it waits in the queue.</summary>
    [JsonIgnore] public DateTimeOffset? DeliveryStartedAt => Current.DeliveryStartedAt;
    /// <summary>Most recent retained completed result. Use the receipt occurrence identity when waiting for one particular display.</summary>
    [JsonIgnore] public NotificationResult? Result => Occurrences.LastOrDefault(item => item.Result is not null)?.Result;
}
/// <summary>Links an accepted sender request to the display returned by its original receipt; retained with that display's history.</summary>
/// <param name="RequestId">Stable incoming Send identity, which can differ from the series identity after a tag upsert.</param>
/// <param name="OccurrenceId">Display identity originally acknowledged for that request.</param>
public sealed record NotificationAcceptance(Guid RequestId, Guid OccurrenceId);
/// <summary>Owns the evidence and result of one display.</summary>
public sealed record NotificationOccurrence
{
    /// <summary>Identity of this display attempt; snooze, recurrence and retry create another occurrence.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();
    /// <summary>Accepted content version used to reject stale render evidence and old interactions.</summary>
    public long ContentRevision { get; set; }
    /// <summary>Earliest queue eligibility timestamp for this occurrence.</summary>
    public DateTimeOffset DueAt { get; set; } = DateTimeOffset.Now;
    /// <summary>Current lifecycle status; new occurrences begin Pending unless scheduling changes it.</summary>
    public NotificationStatus Status { get; set; } = NotificationStatus.Pending;
    /// <summary>Renderer-slot reservation time, or null while not assigned to the renderer.</summary>
    public DateTimeOffset? DeliveryStartedAt { get; set; }
    /// <summary>First matching HTML render evidence, or null before the card is prepared.</summary>
    public DateTimeOffset? RenderedAt { get; set; }
    /// <summary>Beginning of the current expiry pause, or null while its lifetime is advancing.</summary>
    public DateTimeOffset? PausedAt { get; set; }
    /// <summary>Whether the latest matching render receipt placed this card inside the native visible viewport.</summary>
    public bool InViewport { get; set; }
    /// <summary>First confirmed delivery timestamp; null means this occurrence has no visible-delivery evidence.</summary>
    public DateTimeOffset? VisibleAt { get; set; }
    /// <summary>Expiry deadline adjusted for pauses; null means no active expiry deadline.</summary>
    public DateTimeOffset? ExpiresAt { get; set; }
    /// <summary>Completion timestamp, or null while the occurrence accepts interactions.</summary>
    public DateTimeOffset? ClosedAt { get; set; }
    /// <summary>Persisted completion for this exact occurrence; null while awaiting completion.</summary>
    public NotificationResult? Result { get; set; }
    /// <summary>External action attempt state. A pending attempt prevents a second action from being accepted.</summary>
    public ActionOutcome? Action { get; set; }
    /// <summary>Accepted interaction awaiting an external action outcome; retained so restart recovery can mark interruption without replay.</summary>
    public NotificationResult? PendingResult { get; set; }
}
/// <summary>Records an external action outcome.</summary>
/// <param name="Status">Persisted external-effect state.</param>
/// <param name="Error">Technical failure or interruption explanation; null on successful execution.</param>
public sealed record ActionOutcome(ActionStatus Status, string? Error = null);
/// <summary>Contains one persisted result shared by polling, callbacks and history.</summary>
public sealed record NotificationResult
{
    /// <summary>Stable series identity of the completed notification.</summary>
    public Guid Id { get; set; }
    /// <summary>Exact display identity, also returned by its acceptance receipt.</summary>
    public Guid OccurrenceId { get; set; }
    /// <summary>Declared selected action identity, or null for dismissal, expiry and delivery failure without an action.</summary>
    public string? ActionId { get; set; }
    /// <summary>Submitted reply text, or null when no reply was supplied.</summary>
    public string? Reply { get; set; }
    /// <summary>Selected declared checklist identities, initially empty.</summary>
    public string[] CheckedItems { get; set; } = [];
    /// <summary>Completion reason for this occurrence; a later recurrence has its own status and result.</summary>
    public NotificationStatus Status { get; set; }
    /// <summary>External action outcome, or null for completion without an external effect.</summary>
    public ActionOutcome? Action { get; set; }
    /// <summary>Timestamp when the completion result was created, including its local UTC offset.</summary>
    public DateTimeOffset At { get; set; } = DateTimeOffset.Now;
}
/// <summary>Identifies rendered content and its viewport visibility.</summary>
/// <param name="NotificationId">Notification series rendered by the card.</param>
/// <param name="OccurrenceId">Exact display whose evidence is being reported.</param>
/// <param name="ContentRevision">Accepted content version; stale revisions are ignored.</param>
/// <param name="Visible">Whether the card intersects the measured visible viewport.</param>
public sealed record RenderReceipt(Guid NotificationId, Guid OccurrenceId, long ContentRevision, bool Visible);
/// <summary>Contains a machine-readable error and its explanation.</summary>
/// <param name="Code">Rejection category for program decisions.</param>
/// <param name="Message">Technical rejection detail for diagnostics.</param>
public sealed record NotificationFailure(NotificationError Code, string Message);
/// <summary>Transfers a typed operation; partial Update and complete Replace are distinct.</summary>
public sealed record NotificationCommand
{
    /// <summary>Requested management operation. Its default enum value sends a new notification.</summary>
    public NotificationOperation Operation { get; set; }
    /// <summary>Complete request for Send or Replace. Null on Send creates a notification using defaults.</summary>
    public NotificationRequest? Notification { get; set; }
    /// <summary>Partial field changes required by Update; unused by other operations.</summary>
    public NotificationUpdate? Update { get; set; }
    /// <summary>Series identity for a targeted operation; null on List returns all retained series.</summary>
    public Guid? Id { get; set; }
    /// <summary>Optional expected occurrence for stale-operation rejection in occurrence-aware commands.</summary>
    public Guid? OccurrenceId { get; set; }
    /// <summary>Optional Snooze duration. Null uses the first effective snooze choice.</summary>
    public TimeSpan? Delay { get; set; }
    /// <summary>Requested zero-based manual position for Move, clamped to the retained list bounds.</summary>
    public int? Order { get; set; }
}
/// <summary>Acknowledges durable acceptance and identifies the exact occurrence.</summary>
public sealed record NotificationReceipt
{
    /// <summary>True only after the command succeeds; notification mutations are persisted before acknowledgement.</summary>
    public bool Accepted { get; set; }
    /// <summary>Notification series identity when the operation targets or accepts a notification; null for untargeted management commands.</summary>
    public Guid? Id { get; set; }
    /// <summary>Required series identity convenience accessor. Throws InvalidOperationException when this receipt has no Id.</summary>
    [JsonIgnore] public Guid NotificationId => Id ?? throw new InvalidOperationException("This receipt has no notification identity.");
    /// <summary>Exact accepted or completed display identity, when applicable; pass the whole receipt to WaitAsync.</summary>
    public Guid? OccurrenceId { get; set; }
    /// <summary>Typed rejection reason, or null on success. Transport and caller cancellation can still throw before a receipt is received.</summary>
    public NotificationFailure? Failure { get; set; }
    /// <summary>Readable rejection detail from Failure, or null. Use Failure.Code for program decisions.</summary>
    public string? Error => Failure?.Message;
    /// <summary>Independent retained journal data returned by Get or List; null for other operations.</summary>
    public List<NotificationEntry>? Entries { get; set; }
    /// <summary>Host wire contract version, currently 2. The client checks compatibility before interpreting a response.</summary>
    public int ProtocolVersion { get; set; } = 2;
}
