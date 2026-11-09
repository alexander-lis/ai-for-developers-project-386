using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace CalendarApi.Tests;

/// <summary>The server over a built frontend, as in the Docker image.</summary>
public sealed class FrontendTests : IAsyncDisposable
{
    private const string IndexHtml = "<!doctype html><title>Календарь</title>";

    private readonly string _webRoot = Directory.CreateTempSubdirectory("calendar-test-wwwroot-").FullName;
    private readonly WebApplicationFactory<Program> _factory;

    public FrontendTests()
    {
        File.WriteAllText(Path.Combine(_webRoot, "index.html"), IndexHtml);
        Directory.CreateDirectory(Path.Combine(_webRoot, "assets"));
        File.WriteAllText(Path.Combine(_webRoot, "assets", "app.js"), "console.log('app');");
        _factory = new CalendarApiFactory().WithWebHostBuilder(builder => builder.UseWebRoot(_webRoot));
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/book")]
    [InlineData("/owner/meeting-types")]
    public async Task PageOfTheApp_IsIndexHtml(string path)
    {
        var response = await _factory.CreateClient().GetAsync(path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(IndexHtml, await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task BuiltAsset_IsServed()
    {
        var response = await _factory.CreateClient().GetAsync("/assets/app.js");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("console.log('app');", await response.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData("/api")]
    [InlineData("/api/unknown")]
    public async Task UnknownApiPath_IsNotFoundAsProblemDetails(string path)
    {
        var response = await _factory.CreateClient().GetAsync(path);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task WrongMethodOnApiPath_IsNotAPage()
    {
        var response = await _factory.CreateClient().PostAsync("/api/health", null);

        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
    }

    public async ValueTask DisposeAsync()
    {
        await _factory.DisposeAsync();
        Directory.Delete(_webRoot, recursive: true);
    }
}
