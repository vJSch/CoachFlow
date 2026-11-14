using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using CoachFlow.Api.Exercises;
using CoachFlow.Api.Plans;

namespace CoachFlow.Api.Tests;

public class PlanTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private static object Plan(
        object[]? positions = null,
        Guid[]? focusIds = null,
        int duration = 90,
        int? version = null,
        string title = "Dienstagstraining",
        Level? level = Level.Beginner,
        Spread? spread = Spread.Medium) => new
    {
        title,
        participantCount = 12,
        level,
        spread,
        durationMinutes = duration,
        note = "Halle B",
        focusIds = focusIds ?? [],
        positions = positions ?? [],
        version
    };

    private static object NewPosition(Guid exerciseId, int? duration = null, string? note = null) =>
        new { exerciseId, plannedDurationMinutes = duration, note };

    private static object KeepPosition(Guid id, int duration, string note = "") =>
        new { id, plannedDurationMinutes = duration, note };

    private static async Task<ExerciseResponse> ExerciseAsync(HttpClient client, string name, int duration = 15)
    {
        var response = await client.PostAsJsonAsync("/api/exercises",
            new { name, description = $"Beschreibung {name}", defaultDurationMinutes = duration, focusIds = Array.Empty<Guid>() }, Json);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ExerciseResponse>(Json))!;
    }

    private static async Task<PlanResponse> CreateAsync(HttpClient client, object plan)
    {
        var response = await client.PostAsJsonAsync("/api/plans", plan, Json);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<PlanResponse>(Json))!;
    }

    private static async Task<PlanResponse> GetAsync(HttpClient client, Guid id) =>
        (await client.GetFromJsonAsync<PlanResponse>($"/api/plans/{id}", Json))!;

    private static async Task<Guid[]> FocusIdsAsync(HttpClient client, params string[] names)
    {
        var focuses = await client.GetFromJsonAsync<List<FocusResponse>>("/api/focuses", Json);
        return names.Select(n => focuses!.Single(f => f.Name == n).Id).ToArray();
    }

    [Fact]
    public async Task Plans_require_login()
    {
        await using var app = new TestApp();
        var client = app.CreateClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/plans")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/plans", Plan(), Json)).StatusCode);
    }

    [Fact]
    public async Task Create_copies_exercises_in_order_with_default_duration()
    {
        await using var app = new TestApp();
        var client = await app.CreateUserAsync("t@example.org", "Toni");
        var warmup = await ExerciseAsync(client, "Einlaufen", 10);
        var serve = await ExerciseAsync(client, "Aufschlagserie", 20);
        var focusIds = await FocusIdsAsync(client, "Aufschlag", "Annahme");

        var plan = await CreateAsync(client, Plan(
            positions: [NewPosition(warmup.Id), NewPosition(serve.Id, duration: 25, note: "In Zweiergruppen")],
            focusIds: focusIds));

        Assert.Equal(["Einlaufen", "Aufschlagserie"], plan.Positions.Select(p => p.Name));
        Assert.Equal("Beschreibung Einlaufen", plan.Positions[0].Description);
        Assert.Equal(10, plan.Positions[0].PlannedDurationMinutes);
        Assert.Equal(25, plan.Positions[1].PlannedDurationMinutes);
        Assert.Equal("In Zweiergruppen", plan.Positions[1].Note);
        Assert.Equal(warmup.Id, plan.Positions[0].ExerciseId);
        Assert.Equal(["Annahme", "Aufschlag"], plan.Focuses.Select(f => f.Name));
        Assert.Equal(Spread.Medium, plan.Spread);
        Assert.Equal(35, plan.TotalPositionMinutes);
        Assert.Equal(1, plan.Version);
        Assert.Equal("Toni", plan.UpdatedBy);
    }

    [Fact]
    public async Task Duration_deviation_is_only_a_warning()
    {
        await using var app = new TestApp();
        var client = await app.CreateUserAsync("t@example.org");
        var exercise = await ExerciseAsync(client, "Spiel 6 gegen 6", 60);

        var tooShort = await CreateAsync(client, Plan(positions: [NewPosition(exercise.Id)], duration: 90));
        var exact = await CreateAsync(client, Plan(positions: [NewPosition(exercise.Id)], duration: 60));
        var empty = await CreateAsync(client, Plan(duration: 90));

        Assert.Equal(["Die Übungen dauern zusammen 60 Minuten, geplant sind 90 Minuten."], tooShort.Warnings);
        Assert.Empty(exact.Warnings);
        Assert.Empty(empty.Warnings);
    }

    [Fact]
    public async Task At_most_two_distinct_focuses()
    {
        await using var app = new TestApp();
        var client = await app.CreateUserAsync("t@example.org");
        var ids = await FocusIdsAsync(client, "Block", "Angriff", "Zuspiel");

        var three = await client.PostAsJsonAsync("/api/plans", Plan(focusIds: ids), Json);
        var duplicate = await client.PostAsJsonAsync("/api/plans", Plan(focusIds: [ids[0], ids[0]]), Json);
        var unknown = await client.PostAsJsonAsync("/api/plans", Plan(focusIds: [Guid.NewGuid()]), Json);

        Assert.Equal(HttpStatusCode.BadRequest, three.StatusCode);
        Assert.Equal("Ein Plan hat höchstens 2 Schwerpunkte.", await MessageAsync(three));
        Assert.Equal(HttpStatusCode.BadRequest, duplicate.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, unknown.StatusCode);
    }

    [Fact]
    public async Task Required_parameters_are_checked_per_field()
    {
        await using var app = new TestApp();
        var client = await app.CreateUserAsync("t@example.org");

        var response = await client.PostAsJsonAsync("/api/plans", Plan(title: " ", level: null, spread: null, duration: 0), Json);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var fields = body.RootElement.GetProperty("errors").EnumerateObject().Select(p => p.Name).ToHashSet();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Superset(new HashSet<string> { "title", "level", "spread", "durationMinutes" }, fields);
    }

    [Fact]
    public async Task Changing_or_deleting_an_exercise_leaves_plans_unchanged()
    {
        await using var app = new TestApp();
        var client = await app.CreateUserAsync("t@example.org");
        var kept = await ExerciseAsync(client, "Original", 15);
        var removed = await ExerciseAsync(client, "Wird gelöscht", 20);
        var plan = await CreateAsync(client, Plan(positions: [NewPosition(kept.Id), NewPosition(removed.Id)]));

        await client.PutAsJsonAsync($"/api/exercises/{kept.Id}",
            new { name = "Geändert", description = "Neu", defaultDurationMinutes = 40, focusIds = Array.Empty<Guid>(), version = 1 }, Json);
        var delete = await client.DeleteAsync($"/api/exercises/{removed.Id}");
        var after = await GetAsync(client, plan.Id);

        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        Assert.Equal(["Original", "Wird gelöscht"], after.Positions.Select(p => p.Name));
        Assert.Equal("Beschreibung Original", after.Positions[0].Description);
        Assert.Equal(15, after.Positions[0].PlannedDurationMinutes);
        Assert.Equal(kept.Id, after.Positions[0].ExerciseId);
        // Der Verweis wird leer, die Kopie bleibt vollständig.
        Assert.Null(after.Positions[1].ExerciseId);
        Assert.Equal("Beschreibung Wird gelöscht", after.Positions[1].Description);
    }

    [Fact]
    public async Task Update_reorders_edits_removes_and_adds_positions_but_keeps_copies()
    {
        await using var app = new TestApp();
        var client = await app.CreateUserAsync("t@example.org");
        var a = await ExerciseAsync(client, "A", 10);
        var b = await ExerciseAsync(client, "B", 10);
        var c = await ExerciseAsync(client, "C", 10);
        var d = await ExerciseAsync(client, "D", 10);
        var plan = await CreateAsync(client, Plan(positions: [NewPosition(a.Id), NewPosition(b.Id), NewPosition(c.Id)]));
        // Eine Änderung an A nach dem ersten Speichern darf die bestehende Position nicht erreichen.
        await client.PutAsJsonAsync($"/api/exercises/{a.Id}",
            new { name = "A neu", description = "", defaultDurationMinutes = 10, focusIds = Array.Empty<Guid>(), version = 1 }, Json);
        var pa = plan.Positions[0].Id;
        var pc = plan.Positions[2].Id;

        var response = await client.PutAsJsonAsync($"/api/plans/{plan.Id}", Plan(
            positions: [KeepPosition(pc, 30, "zuerst"), NewPosition(d.Id), KeepPosition(pa, 5)],
            version: 1), Json);
        var updated = await response.Content.ReadFromJsonAsync<PlanResponse>(Json);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(["C", "D", "A"], updated!.Positions.Select(p => p.Name));
        Assert.Equal([pc, pa], [updated.Positions[0].Id, updated.Positions[2].Id]);
        Assert.Equal([30, 10, 5], updated.Positions.Select(p => p.PlannedDurationMinutes));
        Assert.Equal("zuerst", updated.Positions[0].Note);
        Assert.Equal(2, updated.Version);
    }

    [Fact]
    public async Task Stale_version_is_a_conflict_and_changes_nothing()
    {
        await using var app = new TestApp();
        var first = await app.CreateUserAsync("erste@example.org", "Erste");
        var second = await app.CreateUserAsync("zweite@example.org", "Zweite");
        var plan = await CreateAsync(first, Plan());

        await second.PutAsJsonAsync($"/api/plans/{plan.Id}", Plan(title: "Von Zweite", version: 1), Json);
        var stale = await first.PutAsJsonAsync($"/api/plans/{plan.Id}", Plan(title: "Von Erste", version: 1), Json);
        using var body = JsonDocument.Parse(await stale.Content.ReadAsStringAsync());
        var current = await GetAsync(first, plan.Id);

        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        Assert.Contains("von Zweite geändert", body.RootElement.GetProperty("detail").GetString());
        Assert.Equal("Von Zweite", body.RootElement.GetProperty("current").GetProperty("title").GetString());
        Assert.Equal("Von Zweite", current.Title);
        Assert.Equal(2, current.Version);
    }

    [Fact]
    public async Task Position_of_another_plan_or_unknown_exercise_is_rejected()
    {
        await using var app = new TestApp();
        var client = await app.CreateUserAsync("t@example.org");
        var exercise = await ExerciseAsync(client, "A");
        var other = await CreateAsync(client, Plan(positions: [NewPosition(exercise.Id)]));
        var plan = await CreateAsync(client, Plan());

        var foreign = await client.PutAsJsonAsync($"/api/plans/{plan.Id}",
            Plan(positions: [KeepPosition(other.Positions[0].Id, 10)], version: 1), Json);
        var unknown = await client.PostAsJsonAsync("/api/plans", Plan(positions: [NewPosition(Guid.NewGuid())]), Json);

        Assert.Equal(HttpStatusCode.BadRequest, foreign.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, unknown.StatusCode);
        Assert.Single((await GetAsync(client, other.Id)).Positions);
    }

    [Fact]
    public async Task List_returns_full_plans_newest_first()
    {
        await using var app = new TestApp();
        var client = await app.CreateUserAsync("t@example.org");
        var exercise = await ExerciseAsync(client, "A");
        await CreateAsync(client, Plan(title: "Älter", positions: [NewPosition(exercise.Id)]));
        app.Time.Advance(TimeSpan.FromHours(1));
        await CreateAsync(client, Plan(title: "Neuer"));

        var plans = await client.GetFromJsonAsync<List<PlanResponse>>("/api/plans", Json);

        Assert.Equal(["Neuer", "Älter"], plans!.Select(p => p.Title));
        Assert.Equal("A", Assert.Single(plans[1].Positions).Name);
    }

    [Fact]
    public async Task Delete_removes_plan()
    {
        await using var app = new TestApp();
        var client = await app.CreateUserAsync("t@example.org");
        var exercise = await ExerciseAsync(client, "A");
        var plan = await CreateAsync(client, Plan(positions: [NewPosition(exercise.Id)]));

        var delete = await client.DeleteAsync($"/api/plans/{plan.Id}");
        var get = await client.GetAsync($"/api/plans/{plan.Id}");

        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, get.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/exercises/{exercise.Id}")).StatusCode);
    }

    private static async Task<string> MessageAsync(HttpResponseMessage response)
    {
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return body.RootElement.GetProperty("errors").EnumerateObject().First().Value[0].GetString()!;
    }
}
