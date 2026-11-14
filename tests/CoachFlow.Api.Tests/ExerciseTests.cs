using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using CoachFlow.Api.Exercises;

namespace CoachFlow.Api.Tests;

public class ExerciseTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private static object NewExercise(
        string name = "Pritschen in Dreiergruppen",
        string description = "Drei Spielende im Dreieck, Ball hoch halten.",
        int duration = 15,
        int? min = null,
        int? max = null,
        Level? level = null,
        Guid[]? focusIds = null,
        int? version = null,
        ExerciseOrigin? origin = null) => new
    {
        name,
        description,
        defaultDurationMinutes = duration,
        minParticipants = min,
        maxParticipants = max,
        level,
        origin,
        focusIds = focusIds ?? [],
        version
    };

    private static async Task<ExerciseResponse> CreateAsync(HttpClient client, object exercise)
    {
        var response = await client.PostAsJsonAsync("/api/exercises", exercise, Json);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ExerciseResponse>(Json))!;
    }

    private static async Task<List<string>> NamesAsync(HttpClient client, string query)
    {
        var list = await client.GetFromJsonAsync<List<ExerciseResponse>>("/api/exercises" + query, Json);
        return list!.Select(e => e.Name).ToList();
    }

    [Fact]
    public async Task Exercises_and_focuses_require_login()
    {
        await using var app = new TestApp();
        var client = app.CreateClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/exercises")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/focuses")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/exercises", NewExercise(), Json)).StatusCode);
    }

    [Fact]
    public async Task Focus_catalog_is_prefilled_and_sorted_german()
    {
        await using var app = new TestApp();
        var client = await app.CreateUserAsync("t@example.org");

        var focuses = await client.GetFromJsonAsync<List<FocusResponse>>("/api/focuses", Json);

        Assert.Equal(FocusCatalog.Initial.Count, focuses!.Count);
        Assert.Contains(focuses, f => f.Name == "Annahme");
        // „Athletik“ vor „Aufschlag“ vor „Aufwärmen“: Umlaute stehen bei ihrem Grundbuchstaben.
        Assert.Equal(["Angriff", "Annahme", "Athletik", "Aufschlag", "Aufwärmen"], focuses.Take(5).Select(f => f.Name));
    }

    [Fact]
    public async Task Users_can_add_focus_but_not_a_variant_of_an_existing_one()
    {
        await using var app = new TestApp();
        var client = await app.CreateUserAsync("t@example.org");

        var created = await client.PostAsJsonAsync("/api/focuses", new { name = "  Läufer   Wechsel " });
        var duplicate = await client.PostAsJsonAsync("/api/focuses", new { name = "läufer wechsel" });
        var seededDuplicate = await client.PostAsJsonAsync("/api/focuses", new { name = "ANNAHME" });
        var empty = await client.PostAsJsonAsync("/api/focuses", new { name = "   " });

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal("Läufer Wechsel", (await created.Content.ReadFromJsonAsync<FocusResponse>())!.Name);
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        Assert.Equal("Den Schwerpunkt „Läufer Wechsel“ gibt es bereits.", await TestApp.DetailAsync(duplicate));
        Assert.Equal(HttpStatusCode.Conflict, seededDuplicate.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, empty.StatusCode);
    }

    [Fact]
    public async Task Create_and_read_exercise_with_focuses()
    {
        await using var app = new TestApp();
        var client = await app.CreateUserAsync("t@example.org", "Toni");
        var focuses = await client.GetFromJsonAsync<List<FocusResponse>>("/api/focuses", Json);
        var pritschen = focuses!.Single(f => f.Name == "Pritschen").Id;

        var created = await CreateAsync(client, NewExercise(min: 3, max: 12, level: Level.Beginner, focusIds: [pritschen]));
        var read = await client.GetFromJsonAsync<ExerciseResponse>($"/api/exercises/{created.Id}", Json);

        Assert.Equal("Pritschen in Dreiergruppen", read!.Name);
        Assert.Equal(15, read.DefaultDurationMinutes);
        Assert.Equal(Level.Beginner, read.Level);
        Assert.Equal(ExerciseOrigin.Own, read.Origin);
        Assert.Equal("Pritschen", Assert.Single(read.Focuses).Name);
        Assert.Equal(1, read.Version);
        Assert.Equal("Toni", read.UpdatedBy);
        Assert.Equal(new DateTime(2026, 11, 1, 12, 0, 0, DateTimeKind.Utc), read.UpdatedAt);
    }

    [Fact]
    public async Task Times_are_sent_as_utc()
    {
        await using var app = new TestApp();
        var client = await app.CreateUserAsync("t@example.org");
        var created = await CreateAsync(client, NewExercise());

        var raw = await client.GetStringAsync($"/api/exercises/{created.Id}");

        Assert.Contains("\"updatedAt\":\"2026-11-01T12:00:00Z\"", raw);
    }

    [Fact]
    public async Task Ai_origin_is_kept()
    {
        await using var app = new TestApp();
        var client = await app.CreateUserAsync("t@example.org");

        var created = await CreateAsync(client, NewExercise(origin: ExerciseOrigin.Ai));
        var raw = await client.GetStringAsync($"/api/exercises/{created.Id}");

        Assert.Equal(ExerciseOrigin.Ai, created.Origin);
        Assert.Contains("\"origin\":\"Ai\"", raw);
    }

    [Theory]
    [InlineData("", 15, null, null, "name")]
    [InlineData("Übung", 0, null, null, "defaultDurationMinutes")]
    [InlineData("Übung", 181, null, null, "defaultDurationMinutes")]
    [InlineData("Übung", 15, 10, 4, "participants")]
    [InlineData("Übung", 15, 0, null, "participants")]
    public async Task Invalid_exercise_is_rejected_per_field(string name, int duration, int? min, int? max, string field)
    {
        await using var app = new TestApp();
        var client = await app.CreateUserAsync("t@example.org");

        var response = await client.PostAsJsonAsync("/api/exercises", NewExercise(name: name, duration: duration, min: min, max: max), Json);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True(body.RootElement.GetProperty("errors").TryGetProperty(field, out _));
    }

    [Theory]
    [InlineData("{ kein json")]
    [InlineData("{\"name\":\"X\",\"defaultDurationMinutes\":15,\"level\":\"Profi\"}")]
    public async Task Malformed_body_is_a_400_not_a_server_error(string body)
    {
        await using var app = new TestApp();
        var client = await app.CreateUserAsync("t@example.org");

        var response = await client.PostAsync("/api/exercises",
            new StringContent(body, System.Text.Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Unknown_focus_is_rejected()
    {
        await using var app = new TestApp();
        var client = await app.CreateUserAsync("t@example.org");

        var response = await client.PostAsJsonAsync("/api/exercises", NewExercise(focusIds: [Guid.NewGuid()]), Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Update_with_current_version_increments_it_and_replaces_focuses()
    {
        await using var app = new TestApp();
        var client = await app.CreateUserAsync("t@example.org");
        var focuses = await client.GetFromJsonAsync<List<FocusResponse>>("/api/focuses", Json);
        var block = focuses!.Single(f => f.Name == "Block").Id;
        var angriff = focuses.Single(f => f.Name == "Angriff").Id;
        var created = await CreateAsync(client, NewExercise(focusIds: [block]));

        var response = await client.PutAsJsonAsync($"/api/exercises/{created.Id}",
            NewExercise(name: "Block gegen Angriff", duration: 20, focusIds: [angriff], version: 1), Json);
        var updated = await response.Content.ReadFromJsonAsync<ExerciseResponse>(Json);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, updated!.Version);
        Assert.Equal("Block gegen Angriff", updated.Name);
        Assert.Equal("Angriff", Assert.Single(updated.Focuses).Name);
    }

    [Fact]
    public async Task Update_with_stale_version_is_a_conflict_and_changes_nothing()
    {
        await using var app = new TestApp();
        var first = await app.CreateUserAsync("erste@example.org", "Erste");
        var second = await app.CreateUserAsync("zweite@example.org", "Zweite");
        var created = await CreateAsync(first, NewExercise());

        await second.PutAsJsonAsync($"/api/exercises/{created.Id}", NewExercise(name: "Von Zweite", version: 1), Json);
        var stale = await first.PutAsJsonAsync($"/api/exercises/{created.Id}", NewExercise(name: "Von Erste", version: 1), Json);
        var current = await first.GetFromJsonAsync<ExerciseResponse>($"/api/exercises/{created.Id}", Json);

        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        Assert.Contains("von Zweite geändert", await TestApp.DetailAsync(stale));
        Assert.Equal("Von Zweite", current!.Name);
        Assert.Equal(2, current.Version);
    }

    [Fact]
    public async Task Update_without_version_is_rejected()
    {
        await using var app = new TestApp();
        var client = await app.CreateUserAsync("t@example.org");
        var created = await CreateAsync(client, NewExercise());

        var response = await client.PutAsJsonAsync($"/api/exercises/{created.Id}", NewExercise(), Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Delete_removes_exercise()
    {
        await using var app = new TestApp();
        var client = await app.CreateUserAsync("t@example.org");
        var created = await CreateAsync(client, NewExercise());

        var delete = await client.DeleteAsync($"/api/exercises/{created.Id}");
        var get = await client.GetAsync($"/api/exercises/{created.Id}");
        var deleteAgain = await client.DeleteAsync($"/api/exercises/{created.Id}");

        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, get.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, deleteAgain.StatusCode);
    }

    [Fact]
    public async Task Filters_treat_empty_level_and_bounds_as_suitable_for_all()
    {
        await using var app = new TestApp();
        var client = await app.CreateUserAsync("t@example.org");
        await CreateAsync(client, NewExercise(name: "Für alle"));
        await CreateAsync(client, NewExercise(name: "Nur Mini", level: Level.MiniVolleyball, max: 8));
        await CreateAsync(client, NewExercise(name: "Fortgeschritten ab 12", level: Level.Advanced, min: 12));

        Assert.Equal(["Für alle", "Nur Mini"], await NamesAsync(client, "?level=MiniVolleyball"));
        Assert.Equal(["Fortgeschritten ab 12", "Für alle"], await NamesAsync(client, "?participants=14"));
        Assert.Equal(["Für alle", "Nur Mini"], await NamesAsync(client, "?participants=6"));
        Assert.Equal(["Für alle"], await NamesAsync(client, "?level=Advanced&participants=6"));
    }

    [Fact]
    public async Task Filter_by_focus_matches_any_selected_focus()
    {
        await using var app = new TestApp();
        var client = await app.CreateUserAsync("t@example.org");
        var focuses = await client.GetFromJsonAsync<List<FocusResponse>>("/api/focuses", Json);
        var block = focuses!.Single(f => f.Name == "Block").Id;
        var aufschlag = focuses.Single(f => f.Name == "Aufschlag").Id;
        var annahme = focuses.Single(f => f.Name == "Annahme").Id;
        await CreateAsync(client, NewExercise(name: "Blockübung", focusIds: [block]));
        await CreateAsync(client, NewExercise(name: "Aufschlagübung", focusIds: [aufschlag, annahme]));
        await CreateAsync(client, NewExercise(name: "Ohne Schwerpunkt"));

        Assert.Equal(["Blockübung"], await NamesAsync(client, $"?focusId={block}"));
        Assert.Equal(["Aufschlagübung", "Blockübung"], await NamesAsync(client, $"?focusId={block}&focusId={annahme}"));
    }

    [Fact]
    public async Task Search_ignores_case_including_umlauts()
    {
        await using var app = new TestApp();
        var client = await app.CreateUserAsync("t@example.org");
        await CreateAsync(client, NewExercise(name: "Übergang Abwehr", description: "Schnelles Umschalten"));
        await CreateAsync(client, NewExercise(name: "Aufschlag", description: "Über das Netz in Zone 5"));
        await CreateAsync(client, NewExercise(name: "Block", description: "Am Netz"));

        Assert.Equal(["Aufschlag", "Übergang Abwehr"], await NamesAsync(client, "?search=%C3%BCber"));
        Assert.Equal(["Übergang Abwehr"], await NamesAsync(client, "?search=UMSCHALTEN"));
    }
}
