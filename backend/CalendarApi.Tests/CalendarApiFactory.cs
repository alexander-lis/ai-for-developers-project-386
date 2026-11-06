using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;

namespace CalendarApi.Tests;

/// <summary>
/// The API on its own temporary SQLite file, deleted on dispose.
/// Not <c>:memory:</c>: concurrency tests need real parallel connections.
/// </summary>
public class CalendarApiFactory : WebApplicationFactory<Program>
{
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

    protected override void ConfigureWebHost(IWebHostBuilder builder) =>
        builder.UseSetting("ConnectionStrings:Calendar", ConnectionString(_databasePath));

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
