using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using CoachFlow.Api.Data;
using CoachFlow.Api.Exercises;
using CoachFlow.Api.Plans;
using CoachFlow.Api.Suggestions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CoachFlow.Api.Tests;

// Steuerbarer Anbieter: Jede Anfrage wartet auf eine Antwort, die der Test vorgibt.
public sealed class ScriptedProvider : IExerciseSuggestionProvider
{
    public List<ProviderInput> Inputs { get; } = [];

    public Func<ProviderInput, CancellationToken, Task<ProviderResult>> Handler { get; set; } =
        (_, _) => Task.FromResult(Valid());

    public TaskCompletionSource Started { get; private set; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public async Task<ProviderResult> SuggestAsync(ProviderInput input, CancellationToken ct)
    {
        Inputs.Add(input);
        Started.TrySetResult();
        return await Handler(input, ct);
    }

    public void ResetStarted() => Started = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public static ProviderResult Valid(decimal cost = 0m, string name = "Pritschen im Kreis") => new(
        $$"""{"suggestions":[{"name":"{{name}}","description":"Alle im Kreis.","durationMinutes":15,"minParticipants":4,"maxParticipants":12}]}""",
        100, 200, cost);

    // Wartet, bis die Anfrage abgebrochen wird, wie ein hängender Dienst.
    public static async Task<ProviderResult> Hang(ProviderInput _, CancellationToken ct)
    {
        await Task.Delay(Timeout.Infinite, ct);
        throw new InvalidOperationException("nicht erreichbar");
    }
}

public class SuggestionTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private static object Request(Guid[]? focusIds = null, Level? level = Level.Beginner) => new
    {
        participantCount = 12,
        level,
        spread = Spread.High,
        durationMinutes = 90,
        focusIds = focusIds ?? []
    };

    private static TestApp App(ScriptedProvider provider, Dictionary<string, string>? settings = null)
    {
        var all = new Dictionary<string, string> { ["Ai:Provider"] = SuggestionProviders.Test };
        foreach (var (key, value) in settings ?? [])
        {
            all[key] = value;
        }

        return new TestApp(settings: all, configureServices: services =>
        {
            services.RemoveAll<IExerciseSuggestionProvider>();
            services.AddSingleton<IExerciseSuggestionProvider>(provider);
        });
    }

    private static Task<HttpResponseMessage> SuggestAsync(HttpClient client, object? request = null, CancellationToken ct = default) =>
        client.PostAsJsonAsync("/api/exercise-suggestions", request ?? Request(), Json, ct);

    private static async Task<List<AiUsage>> UsagesAsync(TestApp app)
    {
        using var scope = app.Factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().AiUsages.AsNoTracking().ToListAsync();
    }

    [Fact]
    public async Task Suggestions_require_login()
    {
        await using var app = App(new ScriptedProvider());

        var response = await SuggestAsync(app.CreateClient());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Disabled_by_default_outside_development()
    {
        await using var app = new TestApp();
        var client = await app.CreateUserAsync("t@example.org");

        var status = await client.GetFromJsonAsync<SuggestionStatusResponse>("/api/exercise-suggestions/status");
        var response = await SuggestAsync(client);

        Assert.False(status!.Enabled);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    [Fact]
    public async Task Returns_checked_suggestions_without_saving_exercises()
    {
        var provider = new ScriptedProvider();
        await using var app = App(provider);
        var client = await app.CreateUserAsync("t@example.org");

        var response = await SuggestAsync(client);
        var body = await response.Content.ReadFromJsonAsync<SuggestionResponse>(Json);
        var exercises = await client.GetFromJsonAsync<List<ExerciseResponse>>("/api/exercises", Json);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(new ExerciseSuggestion("Pritschen im Kreis", "Alle im Kreis.", 15, 4, 12), Assert.Single(body!.Suggestions));
        Assert.True(body.IsTestProvider);
        // Ein Vorschlag allein verändert den Bestand nicht.
        Assert.Empty(exercises!);
        var usage = Assert.Single(await UsagesAsync(app));
        Assert.Equal(AiUsageResult.Success, usage.Result);
        Assert.Equal(300, usage.InputTokens + usage.OutputTokens);
    }

    [Fact]
    public async Task Provider_receives_only_the_allowed_fields()
    {
        var provider = new ScriptedProvider();
        await using var app = App(provider);
        var client = await app.CreateUserAsync("geheim@example.org", "Geheimer Name");
        var focuses = await client.GetFromJsonAsync<List<FocusResponse>>("/api/focuses", Json);
        var ids = focuses!.Where(f => f.Name is "Block" or "Angriff").Select(f => f.Id).ToArray();

        await SuggestAsync(client, Request(ids));
        var input = Assert.Single(provider.Inputs);
        var prompt = SuggestionPrompt.Build(input);

        // Kommt ein Feld hinzu, schlägt dieser Test fehl und erzwingt eine bewusste Entscheidung.
        Assert.Equal(
            ["DurationMinutes", "FocusNames", "Level", "ParticipantCount", "Spread", "SuggestionCount"],
            typeof(ProviderInput).GetProperties().Select(p => p.Name).Order());
        Assert.Equal((12, Level.Beginner, Spread.High, 90, 3),
            (input.ParticipantCount, input.Level, input.Spread, input.DurationMinutes, input.SuggestionCount));
        Assert.Equal(["Angriff", "Block"], input.FocusNames);
        Assert.Contains("12 Teilnehmende", prompt);
        Assert.Contains("Angriff, Block", prompt);
        Assert.DoesNotContain("geheim", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("@", prompt);
    }

    [Fact]
    public async Task Invalid_parameters_are_rejected_before_calling_the_provider()
    {
        var provider = new ScriptedProvider();
        await using var app = App(provider);
        var client = await app.CreateUserAsync("t@example.org");

        var missingLevel = await SuggestAsync(client, Request(level: null));
        var threeFocuses = await SuggestAsync(client, Request([Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()]));

        Assert.Equal(HttpStatusCode.BadRequest, missingLevel.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, threeFocuses.StatusCode);
        Assert.Empty(provider.Inputs);
    }

    [Fact]
    public async Task Second_parallel_request_of_same_user_is_rejected()
    {
        var provider = new ScriptedProvider();
        var release = new TaskCompletionSource<ProviderResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        provider.Handler = (_, _) => release.Task;
        await using var app = App(provider);
        var client = await app.CreateUserAsync("t@example.org");
        var other = await app.CreateUserAsync("andere@example.org");

        var first = SuggestAsync(client);
        await provider.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var second = await SuggestAsync(client);
        provider.Handler = (_, _) => Task.FromResult(ScriptedProvider.Valid());
        var otherUser = await SuggestAsync(other);
        release.SetResult(ScriptedProvider.Valid());

        Assert.Equal(HttpStatusCode.TooManyRequests, second.StatusCode);
        Assert.Contains("läuft bereits eine Anfrage", await TestApp.DetailAsync(second));
        Assert.Equal(HttpStatusCode.OK, otherUser.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await first).StatusCode);
    }

    [Fact]
    public async Task Sixth_request_within_a_minute_is_rejected()
    {
        await using var app = App(new ScriptedProvider());
        var client = await app.CreateUserAsync("t@example.org");

        for (var i = 0; i < 5; i++)
        {
            Assert.Equal(HttpStatusCode.OK, (await SuggestAsync(client)).StatusCode);
            app.Time.Advance(TimeSpan.FromSeconds(5));
        }

        var sixth = await SuggestAsync(client);
        app.Time.Advance(TimeSpan.FromSeconds(36));
        var afterWindow = await SuggestAsync(client);

        Assert.Equal(HttpStatusCode.TooManyRequests, sixth.StatusCode);
        Assert.Equal("35", sixth.Headers.RetryAfter?.Delta?.TotalSeconds.ToString());
        Assert.Equal(HttpStatusCode.OK, afterWindow.StatusCode);
    }

    [Fact]
    public async Task Exhausted_monthly_budget_rejects_until_next_month()
    {
        var provider = new ScriptedProvider { Handler = (_, _) => Task.FromResult(ScriptedProvider.Valid(cost: 0.6m)) };
        await using var app = App(provider, new() { ["Ai:MonthlyBudgetEuro"] = "1.0" });
        var client = await app.CreateUserAsync("t@example.org");

        var first = await SuggestAsync(client);
        var second = await SuggestAsync(client);
        var third = await SuggestAsync(client);
        app.Time.Advance(TimeSpan.FromDays(30));
        // Nach 30 Tagen ist die 14-tägige Anmeldung abgelaufen.
        await client.PostAsJsonAsync("/api/account/login", new { email = "t@example.org", password = TestApp.Password });
        var nextMonth = await SuggestAsync(client);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, third.StatusCode);
        Assert.Contains("Budget", await TestApp.DetailAsync(third));
        Assert.Equal(HttpStatusCode.OK, nextMonth.StatusCode);
        // Die abgelehnte dritte Anfrage hat den Anbieter nicht erreicht.
        Assert.Equal(3, provider.Inputs.Count);
    }

    [Fact]
    public async Task Slow_provider_ends_with_the_agreed_timeout_message()
    {
        var provider = new ScriptedProvider { Handler = ScriptedProvider.Hang };
        await using var app = App(provider, new() { ["Ai:Timeout"] = "00:00:00.300" });
        var client = await app.CreateUserAsync("t@example.org");

        var response = await SuggestAsync(client);

        Assert.Equal(HttpStatusCode.GatewayTimeout, response.StatusCode);
        Assert.Equal(
            "Es konnte rechtzeitig kein Übungsvorschlag erstellt werden. Deine Eingaben bleiben erhalten.",
            await TestApp.DetailAsync(response));
        Assert.Equal(AiUsageResult.TimedOut, Assert.Single(await UsagesAsync(app)).Result);
    }

    [Fact]
    public async Task Cancelling_in_the_app_stops_the_provider_and_still_counts()
    {
        var provider = new ScriptedProvider();
        var providerCancelled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        provider.Handler = async (_, ct) =>
        {
            ct.Register(() => providerCancelled.TrySetResult());
            return await ScriptedProvider.Hang(null!, ct);
        };
        await using var app = App(provider);
        var client = await app.CreateUserAsync("t@example.org");
        using var cancel = new CancellationTokenSource();

        var request = SuggestAsync(client, ct: cancel.Token);
        await provider.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await cancel.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => request);
        await providerCancelled.Task.WaitAsync(TimeSpan.FromSeconds(5));

        // Die Nutzung wird nach dem Abbruch im Hintergrund gespeichert.
        List<AiUsage> usages = [];
        for (var i = 0; i < 50 && usages.Count == 0; i++)
        {
            await Task.Delay(50);
            usages = await UsagesAsync(app);
        }

        Assert.Equal(AiUsageResult.Cancelled, Assert.Single(usages).Result);
    }

    [Theory]
    [InlineData("kein json")]
    [InlineData("""{"suggestions":[]}""")]
    [InlineData("""{"suggestions":[{"name":"","description":"x","durationMinutes":10}]}""")]
    [InlineData("""{"suggestions":[{"name":"Zu lang","description":"x","durationMinutes":999}]}""")]
    [InlineData("""{"vorschlaege":[{"name":"Falsches Feld","durationMinutes":10}]}""")]
    public async Task Unusable_answers_give_an_understandable_error(string json)
    {
        var provider = new ScriptedProvider { Handler = (_, _) => Task.FromResult(new ProviderResult(json, 1, 1, 0m)) };
        await using var app = App(provider);
        var client = await app.CreateUserAsync("t@example.org");

        var response = await SuggestAsync(client);

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        Assert.Contains("keine brauchbare Antwort", await TestApp.DetailAsync(response));
        Assert.Equal(AiUsageResult.InvalidResponse, Assert.Single(await UsagesAsync(app)).Result);
    }

    [Fact]
    public async Task Invalid_entries_are_dropped_and_valid_ones_kept()
    {
        const string json = """
            {"suggestions":[
              {"name":"Gut","description":"ok","durationMinutes":10},
              {"name":"Min größer Max","description":"x","durationMinutes":10,"minParticipants":8,"maxParticipants":4},
              {"name":"Auch gut","description":"ok","durationMinutes":20,"minParticipants":2}
            ]}
            """;
        var provider = new ScriptedProvider { Handler = (_, _) => Task.FromResult(new ProviderResult(json, 1, 1, 0m)) };
        await using var app = App(provider);
        var client = await app.CreateUserAsync("t@example.org");

        var body = await (await SuggestAsync(client)).Content.ReadFromJsonAsync<SuggestionResponse>(Json);

        Assert.Equal(["Gut", "Auch gut"], body!.Suggestions.Select(s => s.Name));
    }

    [Theory]
    [InlineData(ProviderFailure.Overloaded, HttpStatusCode.ServiceUnavailable, "ausgelastet")]
    [InlineData(ProviderFailure.Unavailable, HttpStatusCode.BadGateway, "nicht erreichbar")]
    public async Task Provider_errors_give_understandable_messages(ProviderFailure failure, HttpStatusCode status, string text)
    {
        var provider = new ScriptedProvider { Handler = (_, _) => throw new SuggestionProviderException(failure, "Anbieterfehler") };
        await using var app = App(provider);
        var client = await app.CreateUserAsync("t@example.org");

        var response = await SuggestAsync(client);

        Assert.Equal(status, response.StatusCode);
        Assert.Contains(text, await TestApp.DetailAsync(response));
        Assert.Equal(AiUsageResult.ProviderError, Assert.Single(await UsagesAsync(app)).Result);
    }

    [Fact]
    public async Task Test_provider_returns_labelled_examples()
    {
        await using var app = new TestApp(settings: new Dictionary<string, string>
        {
            ["Ai:Provider"] = SuggestionProviders.Test,
            ["Ai:Test:Delay"] = "00:00:00"
        });
        var client = await app.CreateUserAsync("t@example.org");
        var focuses = await client.GetFromJsonAsync<List<FocusResponse>>("/api/focuses", Json);
        var annahme = focuses!.Single(f => f.Name == "Annahme").Id;

        var body = await (await SuggestAsync(client, Request([annahme]))).Content.ReadFromJsonAsync<SuggestionResponse>(Json);

        Assert.Equal(3, body!.Suggestions.Count);
        Assert.All(body.Suggestions, s => Assert.StartsWith("[Testvorschlag]", s.Description));
        Assert.Contains("Annahme", body.Suggestions[0].Name);
    }
}
