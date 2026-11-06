using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace CalendarApi;

public class CalendarDb(DbContextOptions<CalendarDb> options) : DbContext(options)
{
    public DbSet<MeetingType> MeetingTypes => Set<MeetingType>();

    public DbSet<Meeting> Meetings => Set<Meeting>();

    /// <summary>Intervals of meetings of every meeting type that have not ended by <paramref name="now"/>.</summary>
    public async Task<IReadOnlyCollection<(DateTime Start, DateTime End)>> MeetingsAfter(DateTimeOffset now)
    {
        var after = now.UtcDateTime;
        var meetings = await Meetings.AsNoTracking()
            .Where(m => m.End > after)
            .Select(m => new { m.Start, m.End })
            .ToListAsync();
        return [.. meetings.Select(m => (m.Start, m.End))];
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // Every time is stored in UTC (ADR 0003): SQLite cannot compare DateTimeOffset in SQL.
        configurationBuilder.Properties<DateTime>().HaveConversion<UtcConverter>();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<MeetingType>(type =>
        {
            type.Property(t => t.Name).HasMaxLength(100);
            type.Property(t => t.Description).HasMaxLength(500);
        });

        modelBuilder.Entity<Meeting>(meeting =>
        {
            meeting.Property(m => m.MeetingTypeName).HasMaxLength(100);
            meeting.Property(m => m.GuestName).HasMaxLength(100);
        });
    }

    /// <summary>SQLite keeps no kind: what is read back is marked UTC, as it was written.</summary>
    private sealed class UtcConverter() : ValueConverter<DateTime, DateTime>(
        value => value, value => DateTime.SpecifyKind(value, DateTimeKind.Utc));
}
