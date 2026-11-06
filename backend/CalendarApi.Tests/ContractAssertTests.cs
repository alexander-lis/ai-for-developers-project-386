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

    [Fact]
    public async Task MatchesContract_AcceptsNoBodyWhereContractDescribesNone()
    {
        using var response = new HttpResponseMessage(HttpStatusCode.NoContent)
        {
            RequestMessage = new HttpRequestMessage(HttpMethod.Delete, "/api/meeting-types/1"),
        };

        await response.MatchesContract("/api/meeting-types/{id}");
    }

    [Fact]
    public async Task MatchesContract_FailsOnBodyWhereContractDescribesNone()
    {
        using var response = Response(HttpStatusCode.NoContent, "{}");
        response.RequestMessage = new HttpRequestMessage(HttpMethod.Delete, "/api/meeting-types/1");

        await Assert.ThrowsAsync<TrueException>(() => response.MatchesContract("/api/meeting-types/{id}"));
    }

    private static HttpResponseMessage Response(
        HttpStatusCode status, string body, string mediaType = "application/json") =>
        new(status)
        {
            Content = new StringContent(body, Encoding.UTF8, mediaType),
            RequestMessage = new HttpRequestMessage(HttpMethod.Get, "/api/health"),
        };
}
