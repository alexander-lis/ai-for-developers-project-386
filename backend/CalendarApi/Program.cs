using System.Globalization;
using System.Text.Json.Serialization;
using CalendarApi;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSqlite<CalendarDb>(builder.Configuration.GetConnectionString("Calendar"));

// The only source of "now"; tests stop it with FakeTimeProvider.
builder.Services.AddSingleton(TimeProvider.System);

// Optional fields of the contract are absent rather than null.
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull);

// Every error, including bad JSON and 500, as RFC 9457 Problem Details.
builder.Services.AddCalendarProblemDetails();

// DataAnnotations on request models, checked before the handler runs.
builder.Services.AddValidation();

var app = builder.Build();

app.UseCalendarProblemDetails();

// The built frontend in wwwroot, as in the Docker image; in development Vite serves it instead.
app.UseDefaultFiles();
app.UseStaticFiles();

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

api.MapGet("/meeting-types/{id:guid}/booking-days", async Task<Results<Ok<IReadOnlyList<BookingDay>>, NotFound>> (
    Guid id, CalendarDb db, TimeProvider time) =>
{
    var now = time.GetUtcNow();
    var meetingType = await db.MeetingTypes.FindAsync(id);
    return meetingType is null
        ? TypedResults.NotFound()
        : TypedResults.Ok(CalendarRules.BookingDays(now, meetingType.DurationMinutes, await db.IntervalsNotEndedBy(now)));
});

api.MapDelete("/meeting-types/{id:guid}", async Task<Results<NoContent, NotFound>> (Guid id, CalendarDb db) =>
    await db.MeetingTypes.Where(t => t.Id == id).ExecuteDeleteAsync() == 0
        ? TypedResults.NotFound()
        : TypedResults.NoContent());

// Upcoming meetings of every Guest for both roles: those still in progress are included.
api.MapGet("/meetings", async (CalendarDb db, TimeProvider time) =>
    await db.MeetingsNotEndedBy(time.GetUtcNow()).OrderBy(m => m.Start).ToListAsync());

api.MapPost("/meetings", async Task<Results<Created<Meeting>, NotFound, ValidationProblem, ProblemHttpResult>> (
    MeetingInput input, CalendarDb db, TimeProvider time) =>
{
    var now = time.GetUtcNow();

    // BEGIN IMMEDIATE, the Microsoft.Data.Sqlite default: one writer at a time, so of simultaneous
    // requests the later one sees the earlier meeting (ADR 0003). Every check below stays inside.
    await using var transaction = await db.Database.BeginTransactionAsync();
    var meetingType = await db.MeetingTypes.FindAsync(input.MeetingTypeId);
    if (meetingType is null)
    {
        return TypedResults.NotFound();
    }

    // Before the grid check: a slot of yesterday is a conflict, not a bad request.
    if (input.Start <= now)
    {
        return TypedResults.Problem(statusCode: StatusCodes.Status409Conflict,
            detail: "The slot has already started.");
    }

    var slot = CalendarRules.GridSlot(now, input.Start!.Value, meetingType.DurationMinutes);
    if (slot is null)
    {
        return TypedResults.ValidationProblem(new Dictionary<string, string[]>
        {
            ["start"] = ["The start is not a slot of the meeting type in the booking window."],
        });
    }

    if (CalendarRules.Crosses(slot, await db.IntervalsNotEndedBy(now)))
    {
        return TypedResults.Problem(statusCode: StatusCodes.Status409Conflict,
            detail: "Another meeting crosses the slot.");
    }

    var meeting = Meeting.Book(meetingType, input.GuestName, slot);
    db.Meetings.Add(meeting);
    await db.SaveChangesAsync();
    await transaction.CommitAsync();
    return TypedResults.Created((string?)null, meeting);
});

// Pages of the app are routes of React Router. Paths under /api stay out, so an unknown one is
// a 404 and a wrong method a 405, not the HTML of the app.
app.MapFallbackToFile("{*path:regex(^(?!api(/|$)).*$):nonfile}", "index.html");

app.Run();

record HealthStatus(string Status);
