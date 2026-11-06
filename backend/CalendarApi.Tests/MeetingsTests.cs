using System.Net;
using System.Net.Http.Json;
using System.Text;
using static CalendarApi.Tests.BookingDaysTests;

namespace CalendarApi.Tests;

/// <summary>
/// Booking a slot. The clock stands at <see cref="CalendarApiFactory.Now"/>: Monday 9 November
/// 2026, 10:00 in the Calendar time zone (UTC+3).
/// </summary>
public class MeetingsTests
{
    private const string Path = "/api/meetings";

    /// <summary>Tuesday 10 November 2026, 10:00 in the Calendar time zone.</summary>
    internal static readonly DateTimeOffset TuesdayTen = new(2026, 11, 10, 10, 0, 0, TimeSpan.FromHours(3));

    [Fact]
    public async Task Book_ReturnsMeetingWithMeetingTypeSnapshot()
    {
        await using var api = new CalendarApiFactory();
        var typeId = await AddMeetingType(api, durationMinutes: 60, name: "Знакомство");

        var response = await Post(api.CreateClient(), typeId, "Анна", TuesdayTen);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        await response.MatchesContract(Path);
        var meeting = await response.Content.ReadFromJsonAsync<MeetingBody>();
        Assert.NotNull(meeting);
        Assert.NotEqual(Guid.Empty, meeting.Id);
        Assert.Equal(typeId, meeting.MeetingTypeId);
        Assert.Equal("Знакомство", meeting.MeetingTypeName);
        Assert.Equal("Анна", meeting.GuestName);
        Assert.Equal(new DateTime(2026, 11, 10, 7, 0, 0, DateTimeKind.Utc), meeting.Start);
        Assert.Equal(new DateTime(2026, 11, 10, 8, 0, 0, DateTimeKind.Utc), meeting.End);
        Assert.Equal(DateTimeKind.Utc, meeting.Start.Kind);
    }

    [Fact]
    public async Task Book_TrimsGuestName()
    {
        await using var api = new CalendarApiFactory();
        var typeId = await AddMeetingType(api, durationMinutes: 60);

        var response = await Post(api.CreateClient(), typeId, "  Анна\n", TuesdayTen);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        await response.MatchesContract(Path);
        Assert.Equal("Анна", (await response.Content.ReadFromJsonAsync<MeetingBody>())?.GuestName);
    }

    public static TheoryData<string, string> InvalidInputs => new()
    {
        { """{"guestName":"Анна","start":"2026-11-10T07:00:00Z"}""", "meetingTypeId" },
        { """{"meetingTypeId":"not-a-guid","guestName":"Анна","start":"2026-11-10T07:00:00Z"}""", "meetingTypeId" },
        { """{"meetingTypeId":"{id}","start":"2026-11-10T07:00:00Z"}""", "guestName" },
        { """{"meetingTypeId":"{id}","guestName":"","start":"2026-11-10T07:00:00Z"}""", "guestName" },
        { """{"meetingTypeId":"{id}","guestName":"   ","start":"2026-11-10T07:00:00Z"}""", "guestName" },
        { $$"""{"meetingTypeId":"{id}","guestName":"{{new string('а', 101)}}","start":"2026-11-10T07:00:00Z"}""", "guestName" },
        { """{"meetingTypeId":"{id}","guestName":"Анна"}""", "start" },
        { """{"meetingTypeId":"{id}","guestName":"Анна","start":"завтра"}""", "start" },
    };

    [Theory]
    [MemberData(nameof(InvalidInputs))]
    public async Task Book_RejectsInvalidInputByField(string body, string field)
    {
        await using var api = new CalendarApiFactory();
        var typeId = await AddMeetingType(api, durationMinutes: 60);
        var client = api.CreateClient();

        var response = await client.PostAsync(Path, Json(body.Replace("{id}", typeId.ToString())));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await response.MatchesContract(Path);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblem>();
        Assert.Equal([field], problem?.Errors.Keys);
        // Nothing was booked: the slot is still free.
        Assert.Equal(HttpStatusCode.Created, (await Post(client, typeId, "Анна", TuesdayTen)).StatusCode);
    }

    [Fact]
    public async Task Book_OfMissingMeetingTypeIsNotFound()
    {
        await using var api = new CalendarApiFactory();

        var response = await Post(api.CreateClient(), Guid.NewGuid(), "Анна", TuesdayTen);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        await response.MatchesContract(Path);
    }

    [Fact]
    public async Task Book_SecondRequestForSameSlotIsConflict()
    {
        await using var api = new CalendarApiFactory();
        var typeId = await AddMeetingType(api, durationMinutes: 60);
        var client = api.CreateClient();
        await Book(client, typeId, "Анна", TuesdayTen);

        var response = await Post(client, typeId, "Борис", TuesdayTen);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        await response.MatchesContract(Path);
    }

    [Theory]
    [InlineData("2026-11-09T09:00:00+03:00")] // earlier today
    [InlineData("2026-11-09T10:00:00+03:00")] // exactly now: already started
    [InlineData("2026-11-06T10:00:00+03:00")] // last Friday, outside the window too, still a conflict
    public async Task Book_OfStartedSlotIsConflictNotBadRequest(string start)
    {
        await using var api = new CalendarApiFactory();
        var typeId = await AddMeetingType(api, durationMinutes: 60);

        var response = await Post(api.CreateClient(), typeId, "Анна", DateTimeOffset.Parse(start));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        await response.MatchesContract(Path);
    }

    [Theory]
    [InlineData("2026-11-10T10:15:00+03:00")] // between grid starts of a 60-minute type
    [InlineData("2026-11-10T08:00:00+03:00")] // before working hours
    [InlineData("2026-11-10T18:00:00+03:00")] // would end after working hours
    [InlineData("2026-11-14T10:00:00+03:00")] // Saturday
    [InlineData("2026-11-23T10:00:00+03:00")] // Monday after the booking window
    public async Task Book_OffTheGridIsBadRequestUnderStart(string start)
    {
        await using var api = new CalendarApiFactory();
        var typeId = await AddMeetingType(api, durationMinutes: 60);

        var response = await Post(api.CreateClient(), typeId, "Анна", DateTimeOffset.Parse(start));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await response.MatchesContract(Path);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblem>();
        Assert.Equal(["start"], problem?.Errors.Keys);
    }

    [Theory]
    [InlineData("2026-11-10T07:00:00Z")]
    [InlineData("2026-11-10T12:00:00+05:00")]
    [InlineData("2026-11-20T17:00:00+03:00")] // the last work day of the window, ending at 18:00
    public async Task Book_TakesGridStartWithAnyOffset(string start)
    {
        await using var api = new CalendarApiFactory();
        var typeId = await AddMeetingType(api, durationMinutes: 60);

        var response = await Post(api.CreateClient(), typeId, "Анна", DateTimeOffset.Parse(start));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        await response.MatchesContract(Path);
        var meeting = await response.Content.ReadFromJsonAsync<MeetingBody>();
        Assert.Equal(DateTimeOffset.Parse(start).UtcDateTime, meeting?.Start);
    }

    [Fact]
    public async Task Book_RightAfterAnotherMeetingIsAllowed()
    {
        await using var api = new CalendarApiFactory();
        var typeId = await AddMeetingType(api, durationMinutes: 60);
        var client = api.CreateClient();
        await Book(client, typeId, "Анна", TuesdayTen);

        var response = await Post(client, typeId, "Борис", TuesdayTen.AddHours(1));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        await response.MatchesContract(Path);
    }

    [Fact]
    public async Task Book_CrossingMeetingOfAnotherTypeIsConflict()
    {
        await using var api = new CalendarApiFactory();
        var hour = await AddMeetingType(api, durationMinutes: 60);
        var threeQuarters = await AddMeetingType(api, durationMinutes: 45);
        var client = api.CreateClient();
        await Book(client, hour, "Анна", TuesdayTen);

        // 10:30–11:15 shares half an hour with 10:00–11:00.
        var response = await Post(client, threeQuarters, "Борис", TuesdayTen.AddMinutes(30));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        await response.MatchesContract(Path);
    }

    [Fact]
    public async Task Book_OfSimultaneousCrossingRequestsExactlyOneSucceeds()
    {
        await using var api = new CalendarApiFactory();
        var hour = await AddMeetingType(api, durationMinutes: 60);
        // Every one of these shares 10:15–10:30 on Tuesday with the others.
        (Guid TypeId, DateTimeOffset Start)[] crossing =
        [
            (await AddMeetingType(api, durationMinutes: 30), TuesdayTen),
            (await AddMeetingType(api, durationMinutes: 45), TuesdayTen.AddMinutes(-15)),
            (await AddMeetingType(api, durationMinutes: 120), TuesdayTen.AddHours(-1)),
            (await AddMeetingType(api, durationMinutes: 15), TuesdayTen.AddMinutes(15)),
        ];
        var requests = Enumerable.Repeat((TypeId: hour, Start: TuesdayTen), 12)
            .Concat(crossing).Concat(crossing)
            .ToArray();
        var client = api.CreateClient();

        var responses = await Task.WhenAll(
            requests.Select((request, i) => Post(client, request.TypeId, $"Гость {i}", request.Start)));

        Assert.Equal(20, responses.Length);
        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Created);
        Assert.All(responses, r => Assert.Contains(r.StatusCode, new[] { HttpStatusCode.Created, HttpStatusCode.Conflict }));
        foreach (var response in responses)
        {
            await response.MatchesContract(Path);
        }

        Assert.Single(await List(client));
    }

    [Fact]
    public async Task EditingMeetingType_KeepsMeetingTime()
    {
        await using var api = new CalendarApiFactory();
        var typeId = await AddMeetingType(api, durationMinutes: 60);
        var client = api.CreateClient();
        await Book(client, typeId, "Анна", TuesdayTen);

        var edited = await client.PutAsJsonAsync($"/api/meeting-types/{typeId}",
            new { name = "Разбор", durationMinutes = 30 });
        edited.EnsureSuccessStatusCode();

        // The meeting still ends at 11:00, not at 10:30 by the new duration.
        Assert.Equal(HttpStatusCode.Conflict, (await Post(client, typeId, "Борис", TuesdayTen.AddMinutes(30))).StatusCode);
        Assert.Equal(
            ["09:00–09:30", "09:30–10:00", "11:00–11:30"],
            MoscowTimes((await GetBookingDays(api, typeId))[1])[..3]);
        // The list keeps the name at booking.
        var meeting = Assert.Single(await List(client));
        Assert.Equal("Звонок", meeting.MeetingTypeName);
        Assert.Equal(new DateTime(2026, 11, 10, 8, 0, 0, DateTimeKind.Utc), meeting.End);
    }

    [Fact]
    public async Task DeletingMeetingType_KeepsMeeting()
    {
        await using var api = new CalendarApiFactory();
        var deleted = await AddMeetingType(api, durationMinutes: 60);
        var other = await AddMeetingType(api, durationMinutes: 60);
        var client = api.CreateClient();
        await Book(client, deleted, "Анна", TuesdayTen);

        var response = await client.DeleteAsync($"/api/meeting-types/{deleted}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await Post(client, other, "Борис", TuesdayTen)).StatusCode);
        Assert.DoesNotContain("10:00–11:00", MoscowTimes((await GetBookingDays(api, other))[1]));
        var meeting = Assert.Single(await List(client));
        Assert.Equal((deleted, "Звонок", "Анна"), (meeting.MeetingTypeId, meeting.MeetingTypeName, meeting.GuestName));
    }

    [Fact]
    public async Task List_IsEmptyWithoutMeetings()
    {
        await using var api = new CalendarApiFactory();

        Assert.Empty(await List(api.CreateClient()));
    }

    [Fact]
    public async Task List_SortsMeetingsOfEveryTypeByStart()
    {
        await using var api = new CalendarApiFactory();
        var hour = await AddMeetingType(api, durationMinutes: 60, name: "Знакомство");
        var half = await AddMeetingType(api, durationMinutes: 30, name: "Разбор");
        var client = api.CreateClient();
        // Booked out of order: Thursday, then Tuesday 11:00, then Tuesday 10:00.
        await Book(client, half, "Вера", TuesdayTen.AddDays(2));
        await Book(client, hour, "Борис", TuesdayTen.AddHours(1));
        await Book(client, half, "Анна", TuesdayTen);

        var meetings = await List(client);

        Assert.Equal(
            [
                ("Разбор", "Анна", "2026-11-10T07:00:00Z", "2026-11-10T07:30:00Z"),
                ("Знакомство", "Борис", "2026-11-10T08:00:00Z", "2026-11-10T09:00:00Z"),
                ("Разбор", "Вера", "2026-11-12T07:00:00Z", "2026-11-12T07:30:00Z"),
            ],
            meetings.Select(m => (m.MeetingTypeName, m.GuestName, Utc(m.Start), Utc(m.End))));
        Assert.Equal([half, hour, half], meetings.Select(m => m.MeetingTypeId));
        Assert.Equal(3, meetings.Select(m => m.Id).Distinct().Count());
    }

    [Fact]
    public async Task List_LeavesOutMeetingsThatHaveEnded()
    {
        await using var api = new CalendarApiFactory();
        var typeId = await AddMeetingType(api, durationMinutes: 60);
        var client = api.CreateClient();
        await Book(client, typeId, "Анна", TuesdayTen);
        await Book(client, typeId, "Борис", TuesdayTen.AddHours(1));
        await Book(client, typeId, "Вера", TuesdayTen.AddHours(2));

        // Tuesday 11:30: Анна ended at 11:00, Борис is in progress.
        api.Time.SetUtcNow(TuesdayTen.AddMinutes(90));

        Assert.Equal(["Борис", "Вера"], (await List(client)).Select(m => m.GuestName));

        // Exactly at the end of a meeting it is no longer upcoming.
        api.Time.SetUtcNow(TuesdayTen.AddHours(2));

        Assert.Equal(["Вера"], (await List(client)).Select(m => m.GuestName));
    }

    private static string Utc(DateTime time) =>
        time.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ", System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>Lists upcoming meetings through the API, checked against the contract.</summary>
    internal static async Task<MeetingBody[]> List(HttpClient client)
    {
        var response = await client.GetAsync(Path);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await response.MatchesContract(Path);
        return await response.Content.ReadFromJsonAsync<MeetingBody[]>() ?? [];
    }

    /// <summary>Books a slot through the API and requires a 201.</summary>
    internal static async Task Book(HttpClient client, Guid meetingTypeId, string guestName, DateTimeOffset start)
    {
        var response = await Post(client, meetingTypeId, guestName, start);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    internal static Task<HttpResponseMessage> Post(
        HttpClient client, Guid meetingTypeId, string guestName, DateTimeOffset start) =>
        client.PostAsJsonAsync(Path, new { meetingTypeId, guestName, start });

    private static StringContent Json(string body) => new(body, Encoding.UTF8, "application/json");

    private sealed record ValidationProblem(Dictionary<string, string[]> Errors);

    internal sealed record MeetingBody(
        Guid Id, Guid MeetingTypeId, string MeetingTypeName, string GuestName, DateTime Start, DateTime End);
}
