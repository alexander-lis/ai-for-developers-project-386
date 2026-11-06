using System.Net;

namespace CalendarApi.Tests;

public class HealthTests(CalendarApiFactory factory) : IClassFixture<CalendarApiFactory>
{
    [Fact]
    public async Task Health_ReturnsOk()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await response.MatchesContract("/api/health");
    }
}
