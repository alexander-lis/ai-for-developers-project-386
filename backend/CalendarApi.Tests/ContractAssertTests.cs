using System.Net;
using System.Text;
using Xunit.Sdk;

namespace CalendarApi.Tests;

public class ContractAssertTests
{
    [Fact]
    public async Task MatchesContract_AcceptsBodyFromContract()
    {
        using var response = Response(HttpStatusCode.OK, """{"status":"ok"}""");

        await response.MatchesContract("/api/health");
    }

    [Fact]
    public async Task MatchesContract_FailsOnBodyOutsideContract()
    {
        using var response = Response(HttpStatusCode.OK, """{"status":"broken"}""");

        await Assert.ThrowsAsync<TrueException>(() => response.MatchesContract("/api/health"));
    }

    [Fact]
    public async Task MatchesContract_FailsOnUndescribedStatus()
    {
        using var response = Response(HttpStatusCode.Accepted, """{"status":"ok"}""");

        await Assert.ThrowsAsync<TrueException>(() => response.MatchesContract("/api/health"));
    }

    [Fact]
    public async Task MatchesContract_FailsOnUndescribedContentType()
    {
        using var response = Response(HttpStatusCode.OK, """{"status":"ok"}""", "text/plain");

        await Assert.ThrowsAsync<TrueException>(() => response.MatchesContract("/api/health"));
    }

    private static HttpResponseMessage Response(
        HttpStatusCode status, string body, string mediaType = "application/json") =>
        new(status)
        {
            Content = new StringContent(body, Encoding.UTF8, mediaType),
            RequestMessage = new HttpRequestMessage(HttpMethod.Get, "/api/health"),
        };
}
