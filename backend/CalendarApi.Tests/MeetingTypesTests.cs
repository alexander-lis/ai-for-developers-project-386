using System.Net;
using System.Net.Http.Json;
using System.Text;

namespace CalendarApi.Tests;

public class MeetingTypesTests
{
    [Fact]
    public async Task List_IsEmptyWithoutMeetingTypes()
    {
        await using var api = new CalendarApiFactory();

        var response = await api.CreateClient().GetAsync("/api/meeting-types");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await response.MatchesContract("/api/meeting-types");
        Assert.Empty(await response.Content.ReadFromJsonAsync<MeetingType[]>() ?? []);
    }

    [Fact]
    public async Task List_ReturnsMeetingTypesSortedByName()
    {
        await using var api = new CalendarApiFactory();
        await api.AddMeetingTypes(
            new MeetingType { Name = "Ёлка", DurationMinutes = 30 },
            new MeetingType { Name = "Жюри", Description = "Разбор проекта", DurationMinutes = 90 },
            new MeetingType { Name = "Борщ", DurationMinutes = 15 },
            new MeetingType { Name = "аудит", DurationMinutes = 60 });

        var response = await api.CreateClient().GetAsync("/api/meeting-types");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await response.MatchesContract("/api/meeting-types");
        var types = await response.Content.ReadFromJsonAsync<MeetingType[]>() ?? [];
        Assert.Equal(["аудит", "Борщ", "Ёлка", "Жюри"], types.Select(t => t.Name));
        var jury = types[3];
        Assert.NotEqual(Guid.Empty, jury.Id);
        Assert.Equal("Разбор проекта", jury.Description);
        Assert.Equal(90, jury.DurationMinutes);
    }

    [Fact]
    public async Task List_KeepsMeetingTypesAfterRestart()
    {
        var databasePath = CalendarApiFactory.NewDatabasePath();
        try
        {
            await using (var before = CalendarApiFactory.OnDatabase(databasePath))
            {
                await before.AddMeetingTypes(new MeetingType { Name = "Знакомство", DurationMinutes = 30 });
            }

            await using var after = CalendarApiFactory.OnDatabase(databasePath);
            var types = await after.CreateClient().GetFromJsonAsync<MeetingType[]>("/api/meeting-types") ?? [];

            Assert.Equal(["Знакомство"], types.Select(t => t.Name));
        }
        finally
        {
            CalendarApiFactory.DeleteDatabase(databasePath);
        }
    }

    [Fact]
    public async Task Create_ReturnsMeetingTypeAndListsIt()
    {
        await using var api = new CalendarApiFactory();
        var client = api.CreateClient();

        var response = await client.PostAsJsonAsync("/api/meeting-types",
            new { name = "Знакомство", description = "Первый звонок", durationMinutes = 30 });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        await response.MatchesContract("/api/meeting-types");
        var created = await response.Content.ReadFromJsonAsync<MeetingType>();
        Assert.NotNull(created);
        Assert.NotEqual(Guid.Empty, created.Id);
        Assert.Equal("Знакомство", created.Name);
        Assert.Equal("Первый звонок", created.Description);
        Assert.Equal(30, created.DurationMinutes);
        var listed = await client.GetFromJsonAsync<MeetingType[]>("/api/meeting-types") ?? [];
        Assert.Equal([created.Id], listed.Select(t => t.Id));
    }

    [Fact]
    public async Task Create_TrimsTextAndDropsBlankDescription()
    {
        await using var api = new CalendarApiFactory();
        var client = api.CreateClient();

        var response = await client.PostAsJsonAsync("/api/meeting-types",
            new { name = "  Знакомство ", description = "   ", durationMinutes = 30 });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        await response.MatchesContract("/api/meeting-types");
        var listed = await client.GetFromJsonAsync<MeetingType[]>("/api/meeting-types") ?? [];
        var stored = Assert.Single(listed);
        Assert.Equal("Знакомство", stored.Name);
        Assert.Null(stored.Description);
    }

    [Fact]
    public async Task Create_TrimsDescription()
    {
        await using var api = new CalendarApiFactory();

        var response = await api.CreateClient().PostAsJsonAsync("/api/meeting-types",
            new { name = "Знакомство", description = " Первый звонок\n", durationMinutes = 30 });

        var created = await response.Content.ReadFromJsonAsync<MeetingType>();
        Assert.Equal("Первый звонок", created?.Description);
    }

    public static TheoryData<string, string> InvalidInputs => new()
    {
        { """{"name":"","durationMinutes":30}""", "name" },
        { """{"durationMinutes":30}""", "name" },
        { """{"name":"   ","durationMinutes":30}""", "name" },
        { $$"""{"name":"{{new string('а', 101)}}","durationMinutes":30}""", "name" },
        { $$"""{"name":"Знакомство","description":"{{new string('а', 501)}}","durationMinutes":30}""", "description" },
        { """{"name":"Знакомство","durationMinutes":4}""", "durationMinutes" },
        { """{"name":"Знакомство","durationMinutes":481}""", "durationMinutes" },
        { """{"name":"Знакомство"}""", "durationMinutes" },
    };

    [Theory]
    [MemberData(nameof(InvalidInputs))]
    public async Task Create_RejectsInvalidInputByField(string body, string field)
    {
        await using var api = new CalendarApiFactory();
        var client = api.CreateClient();

        var response = await client.PostAsync("/api/meeting-types", Json(body));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await response.MatchesContract("/api/meeting-types");
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblem>();
        Assert.Equal([field], problem?.Errors.Keys);
        Assert.Empty(await client.GetFromJsonAsync<MeetingType[]>("/api/meeting-types") ?? []);
    }

    [Theory]
    [InlineData("""{"name":"Знакомство","durationMinutes":""")]
    [InlineData("""{"name":"Знакомство","durationMinutes":30.5}""")]
    public async Task Create_RejectsMalformedJson(string body)
    {
        await using var api = new CalendarApiFactory();

        var response = await api.CreateClient().PostAsync("/api/meeting-types", Json(body));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await response.MatchesContract("/api/meeting-types");
    }

    [Fact]
    public async Task Update_ReplacesAllFields()
    {
        await using var api = new CalendarApiFactory();
        var client = api.CreateClient();
        var created = await Create(client,
            new { name = "Знакомство", description = "Первый звонок", durationMinutes = 30 });

        var response = await client.PutAsJsonAsync($"/api/meeting-types/{created.Id}",
            new { name = " Разбор ", durationMinutes = 90 });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await response.MatchesContract("/api/meeting-types/{id}");
        var listed = await client.GetFromJsonAsync<MeetingType[]>("/api/meeting-types") ?? [];
        var stored = Assert.Single(listed);
        Assert.Equal(created.Id, stored.Id);
        Assert.Equal("Разбор", stored.Name);
        Assert.Null(stored.Description);
        Assert.Equal(90, stored.DurationMinutes);
    }

    [Theory]
    [MemberData(nameof(InvalidInputs))]
    public async Task Update_RejectsInvalidInputByField(string body, string field)
    {
        await using var api = new CalendarApiFactory();
        var client = api.CreateClient();
        var created = await Create(client, new { name = "Знакомство", durationMinutes = 30 });

        var response = await client.PutAsync($"/api/meeting-types/{created.Id}", Json(body));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await response.MatchesContract("/api/meeting-types/{id}");
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblem>();
        Assert.Equal([field], problem?.Errors.Keys);
        var stored = Assert.Single(await client.GetFromJsonAsync<MeetingType[]>("/api/meeting-types") ?? []);
        Assert.Equal("Знакомство", stored.Name);
    }

    [Theory]
    [InlineData("3f2b8c1e-0d4a-4b6e-9a51-7c2d9e8f1a30")]
    [InlineData("not-a-guid")]
    public async Task Update_ReturnsNotFoundForUnknownId(string id)
    {
        await using var api = new CalendarApiFactory();
        var client = api.CreateClient();

        var response = await client.PutAsJsonAsync($"/api/meeting-types/{id}",
            new { name = "Знакомство", durationMinutes = 30 });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        await response.MatchesContract("/api/meeting-types/{id}");
        Assert.Empty(await client.GetFromJsonAsync<MeetingType[]>("/api/meeting-types") ?? []);
    }

    [Fact]
    public async Task Delete_RemovesMeetingTypeFromList()
    {
        await using var api = new CalendarApiFactory();
        var client = api.CreateClient();
        var kept = await Create(client, new { name = "Знакомство", durationMinutes = 30 });
        var deleted = await Create(client, new { name = "Разбор", durationMinutes = 90 });

        var response = await client.DeleteAsync($"/api/meeting-types/{deleted.Id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        await response.MatchesContract("/api/meeting-types/{id}");
        var listed = await client.GetFromJsonAsync<MeetingType[]>("/api/meeting-types") ?? [];
        Assert.Equal([kept.Id], listed.Select(t => t.Id));
    }

    [Theory]
    [InlineData("3f2b8c1e-0d4a-4b6e-9a51-7c2d9e8f1a30")]
    [InlineData("not-a-guid")]
    public async Task Delete_ReturnsNotFoundForUnknownId(string id)
    {
        await using var api = new CalendarApiFactory();

        var response = await api.CreateClient().DeleteAsync($"/api/meeting-types/{id}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        await response.MatchesContract("/api/meeting-types/{id}");
    }

    [Fact]
    public async Task Delete_ReturnsNotFoundWhenAlreadyDeleted()
    {
        await using var api = new CalendarApiFactory();
        var client = api.CreateClient();
        var created = await Create(client, new { name = "Знакомство", durationMinutes = 30 });
        await client.DeleteAsync($"/api/meeting-types/{created.Id}");

        var response = await client.DeleteAsync($"/api/meeting-types/{created.Id}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        await response.MatchesContract("/api/meeting-types/{id}");
    }

    private static async Task<MeetingType> Create(HttpClient client, object input)
    {
        var response = await client.PostAsJsonAsync("/api/meeting-types", input);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<MeetingType>()
            ?? throw new InvalidOperationException("Created meeting type has no body.");
    }

    private static StringContent Json(string body) => new(body, Encoding.UTF8, "application/json");

    private sealed record ValidationProblem(Dictionary<string, string[]> Errors);
}
