using System.Net;
using System.Net.Http.Json;

namespace CalendarApi.Tests;

/// <summary>
/// Free slots of a meeting type over the booking window. The clock stands at
/// <see cref="CalendarApiFactory.Now"/>: Monday 9 November 2026, 10:00 in the Calendar time zone.
/// </summary>
public class BookingDaysTests
{
    private const string Path = "/api/meeting-types/{id}/booking-days";

    [Fact]
    public async Task BookingDays_AreFourteenDaysFromToday()
    {
        await using var api = new CalendarApiFactory();
        var id = await AddMeetingType(api, durationMinutes: 30);

        var response = await api.CreateClient().GetAsync($"/api/meeting-types/{id}/booking-days");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await response.MatchesContract(Path);
        var days = await response.Content.ReadFromJsonAsync<BookingDay[]>() ?? [];
        Assert.Equal(
            Enumerable.Range(9, 14).Select(day => new DateOnly(2026, 11, day)),
            days.Select(d => d.Date));
    }

    [Fact]
    public async Task BookingDays_StartTodayInCalendarTimeZone()
    {
        await using var api = new CalendarApiFactory();
        // Still 9 November in UTC, already 01:30 on 10 November in Moscow.
        api.Time.SetUtcNow(new DateTimeOffset(2026, 11, 9, 22, 30, 0, TimeSpan.Zero));
        var id = await AddMeetingType(api, durationMinutes: 30);

        var days = await GetBookingDays(api, id);

        Assert.Equal(new DateOnly(2026, 11, 10), days[0].Date);
    }

    [Fact]
    public async Task Slots_StepByThirtyMinutesFromNine()
    {
        await using var api = new CalendarApiFactory();
        var id = await AddMeetingType(api, durationMinutes: 30);

        var tuesday = (await GetBookingDays(api, id))[1];

        Assert.Equal(new DateOnly(2026, 11, 10), tuesday.Date);
        Assert.Equal(
        [
            "09:00–09:30", "09:30–10:00", "10:00–10:30", "10:30–11:00", "11:00–11:30", "11:30–12:00",
            "12:00–12:30", "12:30–13:00", "13:00–13:30", "13:30–14:00", "14:00–14:30", "14:30–15:00",
            "15:00–15:30", "15:30–16:00", "16:00–16:30", "16:30–17:00", "17:00–17:30", "17:30–18:00",
        ], MoscowTimes(tuesday));
    }

    [Fact]
    public async Task Slots_StepByFortyFiveMinutesAndMayEndAtSix()
    {
        await using var api = new CalendarApiFactory();
        var id = await AddMeetingType(api, durationMinutes: 45);

        var tuesday = (await GetBookingDays(api, id))[1];

        Assert.Equal(
        [
            "09:00–09:45", "09:45–10:30", "10:30–11:15", "11:15–12:00", "12:00–12:45", "12:45–13:30",
            "13:30–14:15", "14:15–15:00", "15:00–15:45", "15:45–16:30", "16:30–17:15", "17:15–18:00",
        ], MoscowTimes(tuesday));
    }

    [Fact]
    public async Task Slots_DoNotRunPastSix()
    {
        await using var api = new CalendarApiFactory();
        var id = await AddMeetingType(api, durationMinutes: 120);

        var tuesday = (await GetBookingDays(api, id))[1];

        // 17:00–19:00 would leave working hours.
        Assert.Equal(["09:00–11:00", "11:00–13:00", "13:00–15:00", "15:00–17:00"], MoscowTimes(tuesday));
    }

    [Fact]
    public async Task Slots_AreOnlyOnWorkDays()
    {
        await using var api = new CalendarApiFactory();
        var id = await AddMeetingType(api, durationMinutes: 60);

        var days = await GetBookingDays(api, id);

        DateOnly[] daysOff = [new(2026, 11, 14), new(2026, 11, 15), new(2026, 11, 21), new(2026, 11, 22)];
        Assert.Equal(daysOff, days.Where(d => d.Slots.Count == 0).Select(d => d.Date));
    }

    [Fact]
    public async Task Slots_TodayStartStrictlyAfterNow()
    {
        await using var api = new CalendarApiFactory();
        var id = await AddMeetingType(api, durationMinutes: 30);

        var today = (await GetBookingDays(api, id))[0];

        // 10:00 itself is "now", so it has already started.
        var times = MoscowTimes(today);
        Assert.Equal("10:30–11:00", times[0]);
        Assert.Equal("17:30–18:00", times[^1]);
        Assert.Equal(15, times.Length);
    }

    [Fact]
    public async Task BookingDays_LeaveOutSlotsCrossingMeetingsOfAnyType()
    {
        await using var api = new CalendarApiFactory();
        var hour = await AddMeetingType(api, durationMinutes: 60);
        var threeQuarters = await AddMeetingType(api, durationMinutes: 45);
        var client = api.CreateClient();
        await MeetingsTests.Book(client, hour, "Анна", MeetingsTests.TuesdayTen);

        var tuesday = (await GetBookingDays(api, threeQuarters))[1];

        // 09:45–10:30 and 10:30–11:15 cross 10:00–11:00; 11:15 starts after it.
        Assert.Equal(
        [
            "09:00–09:45", "11:15–12:00", "12:00–12:45", "12:45–13:30", "13:30–14:15", "14:15–15:00",
            "15:00–15:45", "15:45–16:30", "16:30–17:15", "17:15–18:00",
        ], MoscowTimes(tuesday));
    }

    [Fact]
    public async Task BookingDays_KeepSlotRightAfterMeeting()
    {
        await using var api = new CalendarApiFactory();
        var hour = await AddMeetingType(api, durationMinutes: 60);
        var client = api.CreateClient();
        await MeetingsTests.Book(client, hour, "Анна", MeetingsTests.TuesdayTen);

        var tuesday = (await GetBookingDays(api, hour))[1];

        Assert.Equal(
        [
            "09:00–10:00", "11:00–12:00", "12:00–13:00", "13:00–14:00", "14:00–15:00", "15:00–16:00",
            "16:00–17:00", "17:00–18:00",
        ], MoscowTimes(tuesday));
    }

    [Theory]
    [InlineData("3f2b8c1e-7d4a-4e6b-9a0c-5d1e2f3a4b5c")]
    [InlineData("not-a-guid")]
    public async Task BookingDays_OfMissingMeetingTypeAreNotFound(string id)
    {
        await using var api = new CalendarApiFactory();

        var response = await api.CreateClient().GetAsync($"/api/meeting-types/{id}/booking-days");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        await response.MatchesContract(Path);
    }

    internal static async Task<BookingDay[]> GetBookingDays(CalendarApiFactory api, Guid id)
    {
        var response = await api.CreateClient().GetAsync($"/api/meeting-types/{id}/booking-days");
        await response.MatchesContract(Path);
        return await response.Content.ReadFromJsonAsync<BookingDay[]>() ?? [];
    }

    /// <summary>Slots as "HH:mm–HH:mm" in Moscow, which is UTC+3 all year.</summary>
    internal static string[] MoscowTimes(BookingDay day)
    {
        Assert.All(day.Slots, slot => Assert.Equal(DateTimeKind.Utc, slot.Start.Kind));
        Assert.All(day.Slots, slot => Assert.Equal(DateTimeKind.Utc, slot.End.Kind));
        return [.. day.Slots.Select(slot => $"{slot.Start.AddHours(3):HH:mm}–{slot.End.AddHours(3):HH:mm}")];
    }

    internal static async Task<Guid> AddMeetingType(CalendarApiFactory api, int durationMinutes, string name = "Звонок")
    {
        var meetingType = new MeetingType { Name = name, DurationMinutes = durationMinutes };
        await api.AddMeetingTypes(meetingType);
        return meetingType.Id;
    }
}
