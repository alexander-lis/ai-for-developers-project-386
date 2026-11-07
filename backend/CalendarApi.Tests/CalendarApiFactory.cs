using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Time.Testing;

namespace CalendarApi.Tests;

/// <summary>
/// The API on its own temporary SQLite file, deleted on dispose, with the clock stopped at
/// <see cref="Now"/>. Not <c>:memory:</c>: concurrency tests need real parallel connections.
/// </summary>
public class CalendarApiFactory : WebApplicationFactory<Program>
{
    /// <summary>Monday 9 November 2026, 10:00 in the Calendar time zone.</summary>
    public static readonly DateTimeOffset Now = new(2026, 11, 9, 10, 0, 0, TimeSpan.FromHours(3));

    private readonly string _databasePath;
    private readonly bool _ownsDatabase;

    public CalendarApiFactory()
        : this(NewDatabasePath(), ownsDatabase: true)
    {
    }

    private CalendarApiFactory(string databasePath, bool ownsDatabase)
    {
        _databasePath = databasePath;
        _ownsDatabase = ownsDatabase;
    }

    /// <summary>The server clock; move it with <c>SetUtcNow</c> or <c>Advance</c>.</summary>
    public FakeTimeProvider Time { get; } = new(Now);

    /// <summary>The API on a given file, e.g. to restart it; the caller deletes the file.</summary>
    public static CalendarApiFactory OnDatabase(string databasePath) => new(databasePath, ownsDatabase: false);

    public static string NewDatabasePath() =>
        Path.Combine(Path.GetTempPath(), $"calendar-test-{Guid.NewGuid():N}.db");

    public static void DeleteDatabase(string databasePath)
    {
        // Pooled connections keep the file open.
        SqliteConnection.ClearPool(new SqliteConnection(ConnectionString(databasePath)));
        foreach (var suffix in new[] { "", "-wal", "-shm" })
        {
            File.Delete(databasePath + suffix);
        }
    }

    /// <summary>Stores meeting types directly through the EF Core context.</summary>
    public async Task AddMeetingTypes(params MeetingType[] types)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CalendarDb>();
        db.MeetingTypes.AddRange(types);
        await db.SaveChangesAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:Calendar", ConnectionString(_databasePath));
        builder.ConfigureTestServices(services => services.Replace(ServiceDescriptor.Singleton<TimeProvider>(Time)));
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        GC.SuppressFinalize(this);
        if (_ownsDatabase)
        {
            DeleteDatabase(_databasePath);
        }
    }

    private static string ConnectionString(string databasePath) => $"Data Source={databasePath}";
}
