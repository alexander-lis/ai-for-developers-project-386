using Microsoft.EntityFrameworkCore;

namespace CalendarApi;

public class CalendarDb(DbContextOptions<CalendarDb> options) : DbContext(options)
{
    public DbSet<MeetingType> MeetingTypes => Set<MeetingType>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<MeetingType>(type =>
        {
            type.Property(t => t.Name).HasMaxLength(100);
            type.Property(t => t.Description).HasMaxLength(500);
        });
    }
}
