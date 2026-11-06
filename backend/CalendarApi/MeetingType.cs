namespace CalendarApi;

/// <summary>A template the Owner offers to Guests; see GLOSSARY.md.</summary>
public class MeetingType
{
    public Guid Id { get; set; }

    public required string Name { get; set; }

    public string? Description { get; set; }

    public int DurationMinutes { get; set; }
}
