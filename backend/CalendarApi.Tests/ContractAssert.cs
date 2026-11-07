using System.Text.Json;
using Json.Schema;
using Microsoft.OpenApi;
using Microsoft.OpenApi.Reader;

namespace CalendarApi.Tests;

/// <summary>
/// Checks real server responses against the OpenAPI document generated from the contract.
/// </summary>
public static class ContractAssert
{
    private static readonly Lazy<Task<OpenApiDocument>> Document = new(LoadDocument);

    /// <summary>
    /// Fails unless the contract describes the response status and Content-Type for the operation,
    /// and the body matches the described schema. A status described without content, such as 204,
    /// requires an empty body.
    /// </summary>
    /// <param name="path">Path template as written in the contract, e.g. <c>/api/meeting-types</c>.</param>
    public static async Task MatchesContract(this HttpResponseMessage response, string path)
    {
        var method = response.RequestMessage?.Method
            ?? throw new InvalidOperationException("The response has no request to take the method from.");
        var document = await Document.Value;

        Assert.True(document.Paths.TryGetValue(path, out var pathItem), $"The contract has no path {path}.");
        OpenApiOperation? operation = null;
        Assert.True(
            pathItem.Operations?.TryGetValue(method, out operation) == true,
            $"The contract has no operation {method} {path}.");

        var status = ((int)response.StatusCode).ToString(System.Globalization.CultureInfo.InvariantCulture);
        IOpenApiResponse? described = null;
        Assert.True(
            operation!.Responses?.TryGetValue(status, out described) == true,
            $"The contract does not describe status {status} for {method} {path}.");

        var text = await response.Content.ReadAsStringAsync();
        if (described!.Content is not { Count: > 0 })
        {
            Assert.True(text.Length == 0, $"The contract describes no body for {status} of {method} {path}.");
            return;
        }

        var mediaType = response.Content.Headers.ContentType?.MediaType;
        OpenApiMediaType? content = null;
        Assert.True(
            mediaType is not null && described.Content?.TryGetValue(mediaType, out content) == true,
            $"The contract does not describe Content-Type {mediaType ?? "(none)"} "
            + $"for {status} of {method} {path}.");

        using var body = JsonDocument.Parse(text);
        var results = SchemaOf(content!).Evaluate(
            body.RootElement, new EvaluationOptions { OutputFormat = OutputFormat.List });

        Assert.True(results.IsValid, $"The body of {status} {method} {path} breaks the contract:\n"
            + string.Join("\n", (results.Details ?? [])
                .Where(d => d.Errors is { Count: > 0 })
                .SelectMany(d => d.Errors!.Select(e => $"{d.InstanceLocation}: {e.Key}: {e.Value}"))));
    }

    // The schema as standalone JSON Schema: local $refs are inlined.
    private static JsonSchema SchemaOf(OpenApiMediaType content)
    {
        using var text = new StringWriter();
        var writer = new OpenApiJsonWriter(text, new OpenApiJsonWriterSettings { InlineLocalReferences = true });
        content.Schema!.SerializeAsV31(writer);

        // TypeSpec writes no $schema, and the library default is not 2020-12 (OpenAPI 3.1).
        return JsonSchema.FromText(text.ToString(), new BuildOptions { Dialect = Dialect.Draft202012 });
    }

    private static async Task<OpenApiDocument> LoadDocument()
    {
        var path = ContractPath();
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"{path} is missing: run `mise run generate` first.", path);
        }

        var settings = new OpenApiReaderSettings();
        settings.AddYamlReader();
        await using var file = File.OpenRead(path);
        var (document, diagnostic) = await OpenApiDocument.LoadAsync(file, "yaml", settings);
        Assert.Empty(diagnostic?.Errors ?? []);
        return document!;
    }

    private static string ContractPath()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "mise.toml")))
        {
            dir = dir.Parent;
        }

        return dir is null
            ? throw new DirectoryNotFoundException("Repository root with mise.toml not found.")
            : Path.Combine(dir.FullName, "contract", "tsp-output", "schema", "openapi.yaml");
    }
}
