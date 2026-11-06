using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace CalendarApi;

public class CalendarDb(DbContextOptions<CalendarDb> options) : DbContext(options)
{
    public DbSet<MeetingType> MeetingTypes => Set<MeetingType>();

    public DbSet<Meeting> Meetings => Set<Meeting>();

    /// <summary>
    /// Meetings of every meeting type that have not ended by <paramref name="now"/>: upcoming ones
    /// and those in progress. A meeting that ends exactly at <paramref name="now"/> has ended.
    /// </summary>
    public IQueryable<Meeting> MeetingsNotEndedBy(DateTimeOffset now)
    {
        var utcNow = now.UtcDateTime;
        return Meetings.AsNoTracking().Where(m => m.End > utcNow);
    }

    /// <summary>Intervals of <see cref="MeetingsNotEndedBy"/>.</summary>
    public async Task<IReadOnlyCollection<Interval>> IntervalsNotEndedBy(DateTimeOffset now) =>
        await MeetingsNotEndedBy(now).Select(m => new Interval(m.Start, m.End)).ToListAsync();

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
