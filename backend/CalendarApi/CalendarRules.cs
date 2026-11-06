namespace CalendarApi;

/// <summary>
/// The rules of the single Calendar: its time zone, working hours, work days and booking window.
/// Constants by design (ADR 0002, spec #19): no appsettings, no environment variables.
/// </summary>
public static class CalendarRules
{
    public const string TimeZoneId = "Europe/Moscow";

    public static CalendarSettings Settings { get; } = new(TimeZoneId);
}

/// <summary>What the frontend needs to know about the Calendar.</summary>
public record CalendarSettings(string TimeZone);
