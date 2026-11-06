using System.Globalization;
using System.Text.Json.Serialization;
using CalendarApi;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSqlite<CalendarDb>(builder.Configuration.GetConnectionString("Calendar"));

// Optional fields of the contract are absent rather than null.
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull);

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    scope.ServiceProvider.GetRequiredService<CalendarDb>().Database.Migrate();
}

var api = app.MapGroup("/api");

api.MapGet("/health", () => new HealthStatus("ok"));

// SQLite NOCASE does not know Cyrillic, so sort after loading.
var byName = StringComparer.Create(CultureInfo.GetCultureInfo("ru-RU"), ignoreCase: true);
api.MapGet("/meeting-types", async (CalendarDb db) =>
    (await db.MeetingTypes.AsNoTracking().ToListAsync()).OrderBy(t => t.Name, byName));

app.Run();

record HealthStatus(string Status);
