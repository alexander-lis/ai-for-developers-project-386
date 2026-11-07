using System.ComponentModel.DataAnnotations;

namespace CalendarApi;

/// <summary>A template the Owner offers to Guests; see GLOSSARY.md.</summary>
public class MeetingType
{
    public Guid Id { get; set; }

    public required string Name { get; set; }

    public string? Description { get; set; }

    public int DurationMinutes { get; set; }

    public static MeetingType Create(MeetingTypeInput input)
    {
        var meetingType = new MeetingType { Name = "" };
        meetingType.Replace(input);
        return meetingType;
    }

    /// <summary>Replaces all fields: text trimmed, a blank description stored as null.</summary>
    public void Replace(MeetingTypeInput input)
    {
        Name = input.Name.Trim();
        Description = string.IsNullOrWhiteSpace(input.Description) ? null : input.Description.Trim();
        DurationMinutes = input.DurationMinutes;
    }
}

/// <summary>
/// Body of creating a meeting type or replacing all of its fields.
/// The attributes see the text before trimming.
/// </summary>
public record MeetingTypeInput(
    [Required, MaxLength(100)] string Name,
    [MaxLength(500)] string? Description,
    [Range(5, 480)] int DurationMinutes);
