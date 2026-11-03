using Microsoft.AspNetCore.Mvc.Testing;

namespace CalendarApi.Tests;

public class HealthTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task Health_ReturnsOk()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/health");

        response.EnsureSuccessStatusCode();
        Assert.Equal("""{"status":"ok"}""", await response.Content.ReadAsStringAsync());
    }
}
