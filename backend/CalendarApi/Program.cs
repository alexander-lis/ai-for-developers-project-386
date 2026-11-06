using System.Globalization;
using System.Text.Json.Serialization;
using CalendarApi;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSqlite<CalendarDb>(builder.Configuration.GetConnectionString("Calendar"));

// Optional fields of the contract are absent rather than null.
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull);

// Every error, including bad JSON and 500, as RFC 9457 Problem Details.
builder.Services.AddCalendarProblemDetails();

// DataAnnotations on request models, checked before the handler runs.
builder.Services.AddValidation();

var app = builder.Build();

app.UseCalendarProblemDetails();

using (var scope = app.Services.CreateScope())
{
    scope.ServiceProvider.GetRequiredService<CalendarDb>().Database.Migrate();
}

var api = app.MapGroup("/api");

api.MapGet("/health", () => new HealthStatus("ok"));

api.MapGet("/calendar", () => CalendarRules.Settings);

// SQLite NOCASE does not know Cyrillic, so sort after loading.
var byName = StringComparer.Create(CultureInfo.GetCultureInfo("ru-RU"), ignoreCase: true);
api.MapGet("/meeting-types", async (CalendarDb db) =>
    (await db.MeetingTypes.AsNoTracking().ToListAsync()).OrderBy(t => t.Name, byName));

api.MapPost("/meeting-types", async (MeetingTypeInput input, CalendarDb db) =>
{
    var meetingType = MeetingType.Create(input);
    db.MeetingTypes.Add(meetingType);
    await db.SaveChangesAsync();
    return TypedResults.Created((string?)null, meetingType);
});

// No idempotent outcome: a missing meeting type is 404 for every write.
api.MapPut("/meeting-types/{id:guid}", async Task<Results<Ok<MeetingType>, NotFound>> (
    Guid id, MeetingTypeInput input, CalendarDb db) =>
{
    var meetingType = await db.MeetingTypes.FindAsync(id);
    if (meetingType is null)
    {
        return TypedResults.NotFound();
    }

    meetingType.Replace(input);
    await db.SaveChangesAsync();
    return TypedResults.Ok(meetingType);
});

api.MapDelete("/meeting-types/{id:guid}", async Task<Results<NoContent, NotFound>> (Guid id, CalendarDb db) =>
    await db.MeetingTypes.Where(t => t.Id == id).ExecuteDeleteAsync() == 0
        ? TypedResults.NotFound()
        : TypedResults.NoContent());

app.Run();

record HealthStatus(string Status);
