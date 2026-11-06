namespace CalendarApi;

/// <summary>
/// The rules of the single Calendar: its time zone, working hours, work days and booking window.
/// Constants by design (ADR 0002, spec #19): no appsettings, no environment variables.
/// </summary>
public static class CalendarRules
{
    public const string TimeZoneId = "Europe/Moscow";

    /// <summary>Today and 13 days ahead.</summary>
    public const int BookingWindowDays = 14;

    /// <summary>Working hours, the same on every work day; a slot may end exactly at the end.</summary>
    public static readonly TimeOnly WorkStart = new(9, 0);

    /// <inheritdoc cref="WorkStart"/>
    public static readonly TimeOnly WorkEnd = new(18, 0);

    /// <summary>Monday to Friday.</summary>
    public static readonly IReadOnlySet<DayOfWeek> WorkDays = new HashSet<DayOfWeek>
    {
        DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday,
    };

    public static CalendarSettings Settings { get; } = new(TimeZoneId);

    private static readonly TimeZoneInfo TimeZone = TimeZoneInfo.FindSystemTimeZoneById(TimeZoneId);

    /// <summary>Today in the Calendar time zone.</summary>
    public static DateOnly Today(DateTimeOffset now) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, TimeZone).DateTime);

    /// <summary>
    /// Every day of the booking window in order, each with its free slots: grid slots that start
    /// strictly after <paramref name="now"/> and cross none of <paramref name="meetings"/>.
    /// </summary>
    /// <param name="meetings">Intervals of meetings of any meeting type, in UTC.</param>
    public static IReadOnlyList<BookingDay> BookingDays(
        DateTimeOffset now, int durationMinutes, IReadOnlyCollection<(DateTime Start, DateTime End)> meetings)
    {
        var today = Today(now);
        return [.. Enumerable.Range(0, BookingWindowDays)
            .Select(offset => today.AddDays(offset))
            .Select(day => new BookingDay(day, [.. Grid(day, durationMinutes)
                .Where(slot => slot.Start > now.UtcDateTime && !Crosses(slot, meetings))]))];
    }

    /// <summary>
    /// Whether the slot shares time with a meeting. Touching is fine: a slot may start exactly when a
    /// meeting ends. Checked by intervals, not by equal slots, because grids of types differ.
    /// </summary>
    public static bool Crosses(Slot slot, IEnumerable<(DateTime Start, DateTime End)> meetings) =>
        meetings.Any(meeting => meeting.Start < slot.End && slot.Start < meeting.End);

    /// <summary>
    /// Every slot of a meeting type on a day, regardless of "now" and meetings: from the start of
    /// working hours with a step of the duration (ADR 0002), each wholly inside working hours.
    /// None on days off.
    /// </summary>
    public static IEnumerable<Slot> Grid(DateOnly day, int durationMinutes)
    {
        if (!WorkDays.Contains(day.DayOfWeek))
        {
            yield break;
        }

        var duration = TimeSpan.FromMinutes(durationMinutes);
        for (var start = WorkStart.ToTimeSpan(); start + duration <= WorkEnd.ToTimeSpan(); start += duration)
        {
            yield return new Slot(ToUtc(day, start), ToUtc(day, start + duration));
        }
    }

    private static DateTime ToUtc(DateOnly day, TimeSpan timeOfDay) =>
        TimeZoneInfo.ConvertTimeToUtc(day.ToDateTime(TimeOnly.MinValue).Add(timeOfDay), TimeZone);
}

/// <summary>What the frontend needs to know about the Calendar.</summary>
public record CalendarSettings(string TimeZone);

/// <summary>A span of time a Guest can book; see GLOSSARY.md. Times are UTC.</summary>
public record Slot(DateTime Start, DateTime End);

/// <summary>One day of the booking window with its free slots.</summary>
public record BookingDay(DateOnly Date, IReadOnlyList<Slot> Slots);
