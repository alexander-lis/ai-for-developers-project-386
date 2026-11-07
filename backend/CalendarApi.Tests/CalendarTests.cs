using System.Net;
using System.Text.Json;

namespace CalendarApi.Tests;

public class CalendarTests(CalendarApiFactory factory) : IClassFixture<CalendarApiFactory>
{
    [Fact]
    public async Task Settings_ReturnCalendarTimeZone()
    {
        var response = await factory.CreateClient().GetAsync("/api/calendar");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await response.MatchesContract("/api/calendar");
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("Europe/Moscow", body.RootElement.GetProperty("timeZone").GetString());
    }
}
