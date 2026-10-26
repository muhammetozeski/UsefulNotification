using System.Text.Json.Serialization;
namespace UsefulNotification;

/// <summary>Calculates the next occurrence from an unchanged series anchor.</summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "pattern")]
[JsonDerivedType(typeof(IntervalRecurrence), "interval")]
[JsonDerivedType(typeof(DailyRecurrence), "daily")]
[JsonDerivedType(typeof(WeeklyRecurrence), "weekly")]
[JsonDerivedType(typeof(MonthlyRecurrence), "monthly")]
[JsonDerivedType(typeof(YearlyRecurrence), "yearly")]
public abstract record NotificationRecurrence
{
    /// <summary>Finds the next due time without moving the original series anchor.</summary>
    /// <param name="anchor">Original first due time, including its fixed UTC offset.</param>
    /// <param name="after">Exclusive lower bound; the returned due time is later than this instant.</param>
    /// <returns>The next due time at or after the anchor and strictly after the supplied bound.</returns>
    public abstract DateTimeOffset Next(DateTimeOffset anchor, DateTimeOffset after);
    /// <summary>Rejects recurrence data that cannot produce a valid schedule.</summary>
    /// <exception cref="ArgumentException">The concrete recurrence has an invalid interval or day selection.</exception>
    public virtual void Validate() { }
}
/// <summary>Repeats after a positive fixed duration.</summary>
/// <param name="Every">Positive fixed elapsed duration between anchored occurrences.</param>
public sealed record IntervalRecurrence(TimeSpan Every) : NotificationRecurrence
{
    /// <inheritdoc/>
    public override void Validate() { if (Every <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(Every)); }
    /// <inheritdoc/>
    public override DateTimeOffset Next(DateTimeOffset anchor, DateTimeOffset after)
    {
        Validate();
        return anchor > after ? anchor : anchor.AddTicks(((after - anchor).Ticks / Every.Ticks + 1) * Every.Ticks);
    }
}
/// <summary>Repeats at the anchored local time each day.</summary>
public sealed record DailyRecurrence : NotificationRecurrence
{
    /// <inheritdoc/>
    public override DateTimeOffset Next(DateTimeOffset anchor, DateTimeOffset after)
    {
        after = after.ToOffset(anchor.Offset);
        var next = anchor.AddDays(Math.Max(0, (after.Date - anchor.Date).Days));
        return next > after ? next : next.AddDays(1);
    }
}
/// <summary>Repeats on selected weekdays, or the anchor weekday when Days is empty.</summary>
/// <param name="Days">Selected weekdays; an empty array selects the anchor's weekday. The sending call snapshots the array.</param>
public sealed record WeeklyRecurrence(DayOfWeek[] Days) : NotificationRecurrence
{
    /// <inheritdoc/>
    public override void Validate() { ArgumentNullException.ThrowIfNull(Days); if (Days.Any(day => !Enum.IsDefined(day))) throw new ArgumentException("Invalid weekday."); }
    /// <inheritdoc/>
    public override DateTimeOffset Next(DateTimeOffset anchor, DateTimeOffset after)
    {
        Validate();
        var next = anchor > after ? anchor : new DailyRecurrence().Next(anchor, after);
        var days = Days.Length > 0 ? Days : [anchor.DayOfWeek];
        while (!days.Contains(next.DayOfWeek)) next = next.AddDays(1);
        return next;
    }
}
/// <summary>Repeats on selected month days; an empty list retains and clamps the anchored day.</summary>
/// <param name="Days">Selected day numbers from 1 through 31. Explicit days absent from a month are skipped; an empty array clamps the original anchor day to month end.</param>
public sealed record MonthlyRecurrence(int[] Days) : NotificationRecurrence
{
    /// <inheritdoc/>
    public override void Validate() { ArgumentNullException.ThrowIfNull(Days); if (Days.Any(day => day is < 1 or > 31)) throw new ArgumentException("Month days must be between 1 and 31."); }
    /// <inheritdoc/>
    public override DateTimeOffset Next(DateTimeOffset anchor, DateTimeOffset after)
    {
        Validate();
        after = after.ToOffset(anchor.Offset);
        DateTimeOffset month = anchor > after ? anchor : new(after.Year, after.Month, 1, anchor.Hour, anchor.Minute, anchor.Second, anchor.Offset);
        while (true)
        {
            int limit = DateTime.DaysInMonth(month.Year, month.Month);
            foreach (int day in (Days.Length > 0 ? Days.Where(day => day <= limit) : [Math.Min(anchor.Day, limit)]).Order())
            {
                var candidate = new DateTimeOffset(month.Year, month.Month, day, anchor.Hour, anchor.Minute, anchor.Second, anchor.Offset);
                if (candidate >= anchor && candidate > after) return candidate;
            }
            month = month.AddMonths(1);
        }
    }
}
/// <summary>Repeats at the anchored date and time, clamping leap day when needed.</summary>
public sealed record YearlyRecurrence : NotificationRecurrence
{
    /// <inheritdoc/>
    public override DateTimeOffset Next(DateTimeOffset anchor, DateTimeOffset after)
    {
        after = after.ToOffset(anchor.Offset);
        int year = Math.Max(anchor.Year, after.Year);
        while (true)
        {
            var candidate = new DateTimeOffset(year, anchor.Month, Math.Min(anchor.Day, DateTime.DaysInMonth(year, anchor.Month)), anchor.Hour, anchor.Minute, anchor.Second, anchor.Offset);
            if (candidate >= anchor && candidate > after) return candidate;
            year++;
        }
    }
}
/// <summary>Exposes real reusable recurrence objects.</summary>
public static class NotificationRecurrences
{
    /// <summary>Reusable fixed one-hour recurrence object.</summary>
    public static IntervalRecurrence Hourly = new(TimeSpan.FromHours(1));
    /// <summary>Reusable recurrence retaining the anchor time each day.</summary>
    public static DailyRecurrence Daily = new();
    /// <summary>Reusable Monday-through-Friday recurrence retaining the anchor time.</summary>
    public static WeeklyRecurrence Weekdays = new([DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday]);
    /// <summary>Reusable recurrence on the anchor weekday; its empty Days collection selects that weekday.</summary>
    public static WeeklyRecurrence Weekly = new([]);
    /// <summary>Reusable anchored day-of-month recurrence; dates missing from a shorter month clamp to its last day.</summary>
    public static MonthlyRecurrence Monthly = new([]);
    /// <summary>Reusable anchored annual recurrence; leap-day anchors clamp to February 28 in ordinary years.</summary>
    public static YearlyRecurrence Yearly = new();
}
