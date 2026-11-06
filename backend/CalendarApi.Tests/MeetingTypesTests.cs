using System.Net;
using System.Net.Http.Json;

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
}
