namespace UsefulNotification;

/// <summary>Classifies a notification's meaning and default accent.</summary>
public enum NotificationLevel { Normal, Info, Success, Warning, Error, Urgent, Deadline, Meeting, Health, Break, Personal }
/// <summary>Selects built-in content or a sender-provided HTML template.</summary>
public enum NotificationKind { Text, Reminder, Progress, Image, Choice, Input, Checklist, Custom }
/// <summary>Orders pending cards without replacing an interactive card.</summary>
public enum NotificationPriority { Low, Normal, High, Urgent }
/// <summary>Describes delivery and acknowledgement of a stored notification.</summary>
public enum NotificationStatus { Scheduled, Pending, Visible, Snoozed, Dismissed, Expired, Failed }
/// <summary>Calculates occurrences relative to the series anchor.</summary>
public enum RecurrencePattern { None, Hourly, Daily, Weekdays, Weekly, Monthly, Yearly, Interval }
/// <summary>Determines the effect of a button selected by the user.</summary>
public enum NotificationActionKind { Dismiss, Snooze, OpenUri, RunCommand, Reply, Callback }
/// <summary>Names operations accepted by the local application channel.</summary>
public enum NotificationOperation { Send, Update, Dismiss, Snooze, List, Clear, ShowPanel, ReloadSettings, Shutdown, Get, Delete, Move }

/// <summary>Describes a notification; all fields are optional, including title and message.</summary>
public sealed record NotificationRequest
{
    public Guid? Id { get; set; }
    public string? Source { get; set; }
    public string? Title { get; set; }
    public string? Message { get; set; }
    public NotificationLevel? Level { get; set; }
    public NotificationKind? Kind { get; set; }
    public NotificationPriority? Priority { get; set; }
    public string? Group { get; set; }
    public string? Tag { get; set; }
    public string? Theme { get; set; }
    public NotificationAppearance? Appearance { get; set; }
    public bool? Sticky { get; set; }
    public bool? Silent { get; set; }
    public TimeSpan? Duration { get; set; }
    public ScheduleOptions? Schedule { get; set; }
    public SoundPattern? Sound { get; set; }
    public TimeSpan[]? Snooze { get; set; }
    public List<NotificationAction> Actions { get; set; } = [];
    public ProgressContent? Progress { get; set; }
    public string? Image { get; set; }
    public InputContent? Input { get; set; }
    public List<ChecklistItem> Checklist { get; set; } = [];
    public string? Template { get; set; }
    public Dictionary<string, string> Data { get; set; } = [];
    public string? CallbackPipe { get; set; }
}
/// <summary>Keeps recurrence and delay independent from a card's visible lifetime.</summary>
public sealed record ScheduleOptions
{
    public DateTimeOffset? At { get; set; }
    public TimeSpan? After { get; set; }
    public RecurrencePattern Recurrence { get; set; }
    public TimeSpan? Every { get; set; }
    public DayOfWeek[]? WeeklyDays { get; set; }
    public int[]? MonthlyDays { get; set; }
}
/// <summary>Defines an explicit action executed only after a user selects its button.</summary>
public sealed record NotificationAction
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string? Label { get; set; }
    public NotificationActionKind Kind { get; set; }
    public string? Target { get; set; }
    public string[] Arguments { get; set; } = [];
    public TimeSpan? Snooze { get; set; }
}
/// <summary>Stores determinate or indeterminate progress and its status label.</summary>
public sealed record ProgressContent
{
    public double? Value { get; set; }
    public double Maximum { get; set; } = 100;
    public string? Label { get; set; }
}
/// <summary>Configures a reply field rendered by the built-in card.</summary>
public sealed record InputContent
{
    public string? Placeholder { get; set; }
    public string? Value { get; set; }
    public bool Multiline { get; set; }
    public bool Required { get; set; }
}
/// <summary>Identifies one selectable checklist item.</summary>
public sealed record ChecklistItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Text { get; set; } = string.Empty;
    public bool Checked { get; set; }
}
/// <summary>Stores a named editable tone sequence or a local audio file.</summary>
public sealed record SoundPattern
{
    public string Name { get; set; } = string.Empty;
    public List<ToneStep> Steps { get; set; } = [];
    public string? AudioFile { get; set; }
}
/// <summary>Combines audible frequency, duration, and the following pause.</summary>
/// <param name="Frequency">Frequency in hertz.</param>
/// <param name="Duration">Audible duration.</param>
/// <param name="Pause">Silence following the tone.</param>
public sealed record ToneStep(int Frequency, TimeSpan Duration, TimeSpan Pause);
/// <summary>Persists a request together with its series anchor, delivery evidence, and result.</summary>
public sealed record NotificationEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public NotificationRequest Request { get; set; } = new();
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;
    public DateTimeOffset DueAt { get; set; } = DateTimeOffset.Now;
    public DateTimeOffset SeriesAnchor { get; set; } = DateTimeOffset.Now;
    public DateTimeOffset? VisibleAt { get; set; }
    public DateTimeOffset? ExpiresAt { get; set; }
    public DateTimeOffset? ClosedAt { get; set; }
    public DateTimeOffset? DeliveryStartedAt { get; set; }
    public NotificationStatus Status { get; set; }
    public NotificationResult? Result { get; set; }
    public int Order { get; set; }
}
/// <summary>Captures an action, reply, or checklist selection for the sender.</summary>
public sealed record NotificationResult
{
    public Guid Id { get; set; }
    public string? ActionId { get; set; }
    public string? Reply { get; set; }
    public string[] CheckedItems { get; set; } = [];
    public NotificationStatus Status { get; set; }
    public DateTimeOffset At { get; set; } = DateTimeOffset.Now;
}
/// <summary>Transfers a single operation over the local named pipe.</summary>
public sealed record NotificationCommand
{
    public NotificationOperation Operation { get; set; }
    public NotificationRequest? Notification { get; set; }
    public Guid? Id { get; set; }
    public TimeSpan? Delay { get; set; }
    public int? Order { get; set; }
}
/// <summary>Acknowledges persisted acceptance or explains a rejected request.</summary>
public sealed record NotificationResponse
{
    public bool Accepted { get; set; }
    public Guid? Id { get; set; }
    public string? Error { get; set; }
    public List<NotificationEntry>? Entries { get; set; }
}
