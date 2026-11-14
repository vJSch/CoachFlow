using System.Net;
using System.Net.Http.Json;
using CoachFlow.Application.Exercises;
using CoachFlow.Domain;

namespace CoachFlow.Api.Tests;

public sealed class ApiTests : IDisposable
{
    private static readonly Guid K1 = ExerciseService.SystemFocusAreas[1].Id;
    private readonly ApiFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    private static TrainingParameters Parameters(int participants = 8) =>
        new(participants, ExperienceLevel.Hobby, ExperienceSpread.Keine, 90, [K1]);

    private static ExerciseContent Exercise(string title = "Pritschen", int minimum = 2) =>
        new(title, "Paarweise", 15, minimum, null, [ExperienceLevel.Hobby], [K1], []);

    private static object Plan(string title = "Dienstag", Guid? confirm = null) => new
    {
        title,
        parameters = Parameters(),
        items = new[] { new { sourceExerciseId = (Guid?)null, exercise = Exercise(), plannedMinutes = 15, note = (string?)null } },
        confirmDeletionOf = confirm
    };

    [Fact]
    public async Task Health_check_is_public()
    {
        var response = await _factory.CreateClient().GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Unknown_api_path_is_404_not_the_user_interface()
    {
        var response = await (await _factory.SignedInClientAsync()).GetAsync("/api/gibt-es-nicht");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("not-found", (await response.JsonAsync()).GetProperty("code").GetString());
    }

    [Theory]
    [InlineData("/api/plans")]
    [InlineData("/api/exercises")]
    [InlineData("/api/focus-areas")]
    public async Task Api_requires_sign_in_and_answers_401_instead_of_redirecting(string url)
    {
        var client = _factory.CreateClient(new() { AllowAutoRedirect = false });

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(url)).StatusCode);
    }

    [Fact]
    public async Task Short_password_is_rejected()
    {
        var response = await _factory.CreateClient().PostJsonAsync("/api/auth/register",
            new { email = "kurz@example.org", password = "kurz1" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Session_cookie_is_http_only_and_strict()
    {
        var client = _factory.CreateClient(new() { HandleCookies = false });
        var credentials = new { email = "cookie@example.org", password = "langes-passwort-1" };
        await client.PostJsonAsync("/api/auth/register", credentials);

        var login = await client.PostJsonAsync("/api/auth/login?useCookies=true", credentials);

        var cookie = Assert.Single(login.Headers.GetValues("Set-Cookie"));
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", cookie, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Logout_ends_the_session()
    {
        var client = await _factory.SignedInClientAsync();

        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync("/api/auth/logout", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/plans")).StatusCode);
    }

    [Fact]
    public async Task Focus_areas_include_the_system_values()
    {
        var client = await _factory.SignedInClientAsync();

        var names = (await (await client.GetAsync("/api/focus-areas")).JsonAsync())
            .EnumerateArray().Select(f => f.GetProperty("name").GetString()).ToList();

        Assert.Equal(["K0", "K1", "K2", "Abwehr", "Mannschaftstaktik", "Einzeltaktik"], names);
    }

    [Fact]
    public async Task Exercises_are_shared_between_users()
    {
        var anna = await _factory.SignedInClientAsync();
        var ben = await _factory.SignedInClientAsync();

        var created = await anna.PostJsonAsync("/api/exercises", Exercise());
        var id = (await created.JsonAsync()).GetProperty("id").GetGuid();
        var update = await ben.PutJsonAsync($"/api/exercises/{id}", new { version = 1, content = Exercise("Von Ben") });

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        var listed = (await (await anna.GetAsync("/api/exercises")).JsonAsync()).EnumerateArray().Single();
        Assert.Equal("Von Ben", listed.GetProperty("content").GetProperty("title").GetString());
    }

    [Fact]
    public async Task Stale_exercise_update_returns_conflict_with_message()
    {
        var client = await _factory.SignedInClientAsync();
        var id = (await (await client.PostJsonAsync("/api/exercises", Exercise())).JsonAsync()).GetProperty("id").GetGuid();
        await client.PutJsonAsync($"/api/exercises/{id}", new { version = 1, content = Exercise("Zuerst") });

        var stale = await client.PutJsonAsync($"/api/exercises/{id}", new { version = 1, content = Exercise("Veraltet") });

        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        Assert.Equal("version-conflict", (await stale.JsonAsync()).GetProperty("code").GetString());
    }

    [Fact]
    public async Task Invalid_exercise_returns_the_reasons()
    {
        var client = await _factory.SignedInClientAsync();

        var response = await client.PostJsonAsync("/api/exercises", Exercise() with { Title = "", MinParticipants = null });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(2, (await response.JsonAsync()).GetProperty("errors").GetArrayLength());
    }

    [Fact]
    public async Task Plans_are_private_to_their_owner()
    {
        var anna = await _factory.SignedInClientAsync();
        var ben = await _factory.SignedInClientAsync();
        var id = (await (await anna.PostJsonAsync("/api/plans", Plan())).JsonAsync()).GetProperty("id").GetGuid();

        Assert.Equal(HttpStatusCode.OK, (await anna.GetAsync($"/api/plans/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await ben.GetAsync($"/api/plans/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await ben.DeleteAsync($"/api/plans/{id}")).StatusCode);
        Assert.Equal(0, (await (await ben.GetAsync("/api/plans")).JsonAsync()).GetArrayLength());
    }

    [Fact]
    public async Task Plan_exports_as_xlsx_only_for_its_owner()
    {
        var anna = await _factory.SignedInClientAsync();
        var ben = await _factory.SignedInClientAsync();
        var id = (await (await anna.PostJsonAsync("/api/plans", Plan("U16: Annahme/Abwehr?"))).JsonAsync()).GetProperty("id").GetGuid();

        var response = await anna.GetAsync($"/api/plans/{id}/export");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", response.Content.Headers.ContentType!.MediaType);
        Assert.Equal("CoachFlow - U16_ Annahme_Abwehr_.xlsx", response.Content.Headers.ContentDisposition!.FileNameStar);
        var bytes = await response.Content.ReadAsByteArrayAsync();
        Assert.Equal("PK"u8.ToArray(), bytes[..2]); // xlsx ist ein ZIP-Archiv

        using var workbook = new ClosedXML.Excel.XLWorkbook(new MemoryStream(bytes));
        Assert.Equal("U16: Annahme/Abwehr?", workbook.Worksheet("Plan").Cell(1, 1).GetString());
        Assert.Contains(workbook.Worksheet("Plan").RowsUsed(), r => r.Cell(1).GetString() == "Schwerpunkte" && r.Cell(2).GetString() == "K1");

        Assert.Equal(HttpStatusCode.NotFound, (await ben.GetAsync($"/api/plans/{id}/export")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _factory.CreateClient().GetAsync($"/api/plans/{id}/export")).StatusCode);
    }

    [Fact]
    public async Task Eleventh_plan_needs_confirmation_of_the_named_plan()
    {
        var client = await _factory.SignedInClientAsync();
        for (var i = 1; i <= 10; i++) (await client.PostJsonAsync("/api/plans", Plan($"Plan {i}"))).EnsureSuccessStatusCode();

        var warning = await client.PostJsonAsync("/api/plans", Plan("Plan 11"));
        var body = await warning.JsonAsync();
        var oldestId = body.GetProperty("planToDelete").GetProperty("id").GetGuid();

        Assert.Equal(HttpStatusCode.Conflict, warning.StatusCode);
        Assert.Equal("plan-limit-confirmation", body.GetProperty("code").GetString());
        Assert.Contains("Plan 1", body.GetProperty("message").GetString());

        var confirmed = await client.PostJsonAsync("/api/plans", Plan("Plan 11", oldestId));
        Assert.Equal(HttpStatusCode.Created, confirmed.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/plans/{oldestId}")).StatusCode);
        Assert.Equal(10, (await (await client.GetAsync("/api/plans")).JsonAsync()).GetArrayLength());
    }

    [Fact]
    public async Task Plan_response_contains_duration_and_findings_with_enums_as_text()
    {
        var client = await _factory.SignedInClientAsync();
        var plan = new
        {
            title = "Zu lang",
            parameters = Parameters(participants: 6),
            items = new[] { new { exercise = Exercise(minimum: 12), plannedMinutes = 100 } }
        };

        var body = await (await client.PostJsonAsync("/api/plans", plan)).JsonAsync();

        Assert.Equal(100, body.GetProperty("totalMinutes").GetInt32());
        Assert.Equal("Hobby", body.GetProperty("parameters").GetProperty("level").GetString());
        var codes = body.GetProperty("findings").EnumerateArray().Select(f => f.GetProperty("code").GetString()).ToList();
        Assert.Contains(FindingCodes.PlanDurationExceeded, codes);
        Assert.Contains(FindingCodes.TooFewParticipants, codes);
    }

    [Fact]
    public async Task Suggestion_can_be_requested_and_adopted()
    {
        var client = await _factory.SignedInClientAsync();

        var created = await client.PostJsonAsync("/api/suggestions", new { parameters = Parameters() });
        var suggestion = await created.JsonAsync();
        var id = suggestion.GetProperty("id").GetGuid();
        var adopted = await client.PostAsync($"/api/suggestions/{id}/adopt", null);

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal("Pending", suggestion.GetProperty("status").GetString());
        Assert.Equal(HttpStatusCode.OK, adopted.StatusCode);
        Assert.Equal("Ai", (await adopted.JsonAsync()).GetProperty("origin").GetString());
        Assert.Equal(1, (await (await client.GetAsync("/api/exercises")).JsonAsync()).GetArrayLength());
    }

    [Fact]
    public async Task Conflicting_suggestion_is_blocked_until_corrected()
    {
        var client = await _factory.SignedInClientAsync();
        var id = (await (await client.PostJsonAsync("/api/suggestions", new { parameters = Parameters(participants: 6) })).JsonAsync())
            .GetProperty("id").GetGuid();

        var edited = await client.PutJsonAsync($"/api/suggestions/{id}", new { draft = Exercise(minimum: 12) });
        var blocked = await client.PostAsync($"/api/suggestions/{id}/adopt", null);
        await client.PutJsonAsync($"/api/suggestions/{id}", new { draft = Exercise(minimum: 4) });
        var adopted = await client.PostAsync($"/api/suggestions/{id}/adopt", null);

        Assert.Contains((await edited.JsonAsync()).EnumerateArray(), f => f.GetProperty("code").GetString() == FindingCodes.TooFewParticipants);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, blocked.StatusCode);
        Assert.Equal(HttpStatusCode.OK, adopted.StatusCode);
    }

    [Fact]
    public async Task Unknown_focus_area_never_reaches_the_ai()
    {
        var client = await _factory.SignedInClientAsync();
        var parameters = Parameters() with { FocusAreaIds = [Guid.NewGuid()] };

        var response = await client.PostJsonAsync("/api/suggestions", new { parameters });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Sixth_suggestion_within_a_minute_returns_429()
    {
        var client = await _factory.SignedInClientAsync();
        for (var i = 0; i < 5; i++)
            Assert.Equal(HttpStatusCode.Created, (await client.PostJsonAsync("/api/suggestions", new { parameters = Parameters() })).StatusCode);

        var sixth = await client.PostJsonAsync("/api/suggestions", new { parameters = Parameters() });

        Assert.Equal(HttpStatusCode.TooManyRequests, sixth.StatusCode);
        Assert.Equal("rateLimited", (await sixth.JsonAsync()).GetProperty("code").GetString());
    }
}

public sealed class AiDisabledTests : IDisposable
{
    private readonly ApiFactory _factory = new(aiProvider: "None");

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task Without_provider_ai_is_off_but_planning_works()
    {
        var client = await _factory.SignedInClientAsync();
        var parameters = new TrainingParameters(8, ExperienceLevel.Hobby, ExperienceSpread.Keine, 90, [ExerciseService.SystemFocusAreas[0].Id]);

        var suggestion = await client.PostJsonAsync("/api/suggestions", new { parameters });
        var plan = await client.PostJsonAsync("/api/plans", new { title = "Ohne KI", parameters, items = Array.Empty<object>() });

        Assert.Equal(HttpStatusCode.ServiceUnavailable, suggestion.StatusCode);
        var error = await suggestion.JsonAsync();
        Assert.Equal("disabled", error.GetProperty("code").GetString());
        Assert.Contains("weiterplanen", error.GetProperty("message").GetString());
        Assert.Equal(HttpStatusCode.Created, plan.StatusCode);
    }
}
