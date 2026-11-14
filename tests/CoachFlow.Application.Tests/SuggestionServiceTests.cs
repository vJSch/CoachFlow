using CoachFlow.AiAdapters.Canned;
using CoachFlow.Application.Ai;
using CoachFlow.Application.Exercises;
using CoachFlow.Application.Suggestions;
using CoachFlow.Domain;
using Microsoft.Extensions.Time.Testing;

namespace CoachFlow.Application.Tests;

public class SuggestionServiceTests
{
    private const string Month = "2026-11";

    private static readonly Guid User = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid Focus = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 11, 4, 12, 0, 0, TimeSpan.Zero));
    private readonly InMemoryBudgetLedger _ledger = new();
    private readonly InMemoryExercises _exercises = new();
    private readonly InMemorySuggestions _suggestions = new();

    private SuggestionService Service(IExerciseSuggestionProvider provider, decimal budget = 10m)
    {
        var options = new AiOptions { MonthlyBudget = budget };
        return new SuggestionService(
            provider,
            new AiBudgetGuard(_ledger, options, _time),
            new AiRequestGate(options, _time),
            _suggestions,
            _exercises,
            new NoOpUnitOfWork(),
            options,
            _time);
    }

    private static SuggestionRequest Request(int participants = 6, ExperienceSpread spread = ExperienceSpread.Keine) =>
        new(new TrainingParameters(participants, ExperienceLevel.Hobby, spread, 90, [Focus]), ["K1"]);

    private static ExerciseContent Draft(int minimum = 4) =>
        new("Annahme", "Beschreibung", 15, minimum, null, [ExperienceLevel.Hobby], [Focus], []);

    private static RequestOutcome.Denied AssertDenied(RequestOutcome outcome, RequestDenial reason)
    {
        var denied = Assert.IsType<RequestOutcome.Denied>(outcome);
        Assert.Equal(reason, denied.Reason);
        Assert.False(string.IsNullOrWhiteSpace(denied.Message));
        return denied;
    }

    /// <summary>Begrenzt das Warten, damit ein Fehler den Test scheitern lässt, statt ihn hängen zu lassen.</summary>
    private static async Task WaitUntil(Func<bool> condition)
    {
        using var limit = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        while (!condition())
        {
            Assert.False(limit.IsCancellationRequested, "Bedingung wurde nicht rechtzeitig erfüllt.");
            await Task.Delay(5, CancellationToken.None);
        }
    }

    [Fact]
    public async Task Successful_request_creates_a_suggestion_but_no_exercise()
    {
        var outcome = await Service(CannedSuggestionProvider.WithValidAnswer()).RequestAsync(User, Request(), default);

        var created = Assert.IsType<RequestOutcome.Created>(outcome);
        Assert.Equal(SuggestionStatus.Pending, created.Suggestion.Status);
        Assert.Empty(_exercises.Items);
    }

    [Theory]
    [InlineData(ExperienceSpread.Keine)]
    [InlineData(ExperienceSpread.Anpassungen)]
    [InlineData(ExperienceSpread.Varianten)]
    public async Task Default_canned_answer_is_valid_for_every_spread(ExperienceSpread spread)
    {
        var outcome = await Service(CannedSuggestionProvider.WithValidAnswer()).RequestAsync(User, Request(spread: spread), default);

        Assert.Empty(Assert.IsType<RequestOutcome.Created>(outcome).Suggestion.Findings);
    }

    [Fact]
    public async Task Hanging_provider_times_out_with_the_agreed_message()
    {
        var pending = Service(CannedSuggestionProvider.Hanging()).RequestAsync(User, Request(), default);

        _time.Advance(TimeSpan.FromSeconds(12.9));
        Assert.False(pending.IsCompleted);

        _time.Advance(TimeSpan.FromSeconds(0.2));
        var denied = AssertDenied(await pending, RequestDenial.TimedOut);

        Assert.Equal("Es konnte rechtzeitig kein Übungsvorschlag erstellt werden. Deine Eingaben bleiben erhalten.", denied.Message);
    }

    [Fact]
    public async Task Provider_ignoring_cancellation_still_times_out()
    {
        var pending = Service(CannedSuggestionProvider.IgnoringCancellation()).RequestAsync(User, Request(), default);

        _time.Advance(TimeSpan.FromSeconds(13));

        AssertDenied(await pending, RequestDenial.TimedOut);
    }

    [Fact]
    public async Task User_can_cancel_a_running_request()
    {
        using var cancellation = new CancellationTokenSource();
        var pending = Service(CannedSuggestionProvider.Hanging()).RequestAsync(User, Request(), cancellation.Token);

        await cancellation.CancelAsync();

        AssertDenied(await pending, RequestDenial.Cancelled);
    }

    [Theory]
    [InlineData(AiFailureKind.Unavailable, RequestDenial.ProviderFailed)]
    [InlineData(AiFailureKind.RateLimitedByProvider, RequestDenial.ProviderFailed)]
    [InlineData(AiFailureKind.InvalidResponse, RequestDenial.InvalidResponse)]
    public async Task Provider_failure_becomes_a_user_message(AiFailureKind kind, RequestDenial expected)
    {
        var outcome = await Service(CannedSuggestionProvider.Failing(kind)).RequestAsync(User, Request(), default);

        AssertDenied(outcome, expected);
    }

    [Fact]
    public async Task Unexpected_exception_becomes_a_user_message()
    {
        var outcome = await Service(CannedSuggestionProvider.Throwing()).RequestAsync(User, Request(), default);

        AssertDenied(outcome, RequestDenial.ProviderFailed);
    }

    [Fact]
    public async Task Without_budget_the_provider_is_never_called()
    {
        var provider = CannedSuggestionProvider.WithValidAnswer();

        var outcome = await Service(provider, budget: 0m).RequestAsync(User, Request(), default);

        AssertDenied(outcome, RequestDenial.Disabled);
        Assert.Equal(0, provider.CallCount);
    }

    [Fact]
    public async Task Exhausted_budget_stops_before_the_provider_is_called()
    {
        var provider = CannedSuggestionProvider.WithValidAnswer();

        var outcome = await Service(provider, budget: 0.05m).RequestAsync(User, Request(), default);

        AssertDenied(outcome, RequestDenial.BudgetExhausted);
        Assert.Equal(0, provider.CallCount);
    }

    [Fact]
    public async Task Actual_cost_replaces_the_reservation()
    {
        await Service(CannedSuggestionProvider.WithValidAnswer()).RequestAsync(User, Request(), default);

        Assert.Equal(0.02m, _ledger.Total(Month));
    }

    [Fact]
    public async Task Unknown_cost_keeps_the_reservation()
    {
        await Service(CannedSuggestionProvider.Returning(Draft(), cost: null)).RequestAsync(User, Request(), default);

        Assert.Equal(0.10m, _ledger.Total(Month));
    }

    [Fact]
    public async Task Timed_out_request_keeps_the_reservation()
    {
        var pending = Service(CannedSuggestionProvider.Hanging()).RequestAsync(User, Request(), default);
        _time.Advance(TimeSpan.FromSeconds(13));
        await pending;

        Assert.Equal(0.10m, _ledger.Total(Month));
    }

    [Fact]
    public async Task Parallel_users_cannot_exceed_the_budget()
    {
        var provider = CannedSuggestionProvider.Hanging();
        var service = Service(provider, budget: 0.35m);

        var pending = Enumerable.Range(0, 50)
            .Select(_ => Task.Run(() => service.RequestAsync(Guid.NewGuid(), Request(), default)))
            .ToArray();
        await WaitUntil(() => pending.Count(t => t.IsCompleted) >= 47);

        Assert.True(_ledger.Total(Month) <= 0.35m);
        _time.Advance(TimeSpan.FromSeconds(13));
        var outcomes = await Task.WhenAll(pending);

        Assert.Equal(47, outcomes.Count(o => o is RequestOutcome.Denied { Reason: RequestDenial.BudgetExhausted }));
        Assert.Equal(3, outcomes.Count(o => o is RequestOutcome.Denied { Reason: RequestDenial.TimedOut }));
    }

    [Fact]
    public async Task Second_parallel_request_of_a_user_is_rejected()
    {
        var service = Service(CannedSuggestionProvider.Hanging());
        var first = service.RequestAsync(User, Request(), default);

        AssertDenied(await service.RequestAsync(User, Request(), default), RequestDenial.AlreadyRunning);

        _time.Advance(TimeSpan.FromSeconds(13));
        await first;
    }

    [Fact]
    public async Task Sixth_request_per_minute_is_rejected_and_allowed_after_a_minute()
    {
        var service = Service(CannedSuggestionProvider.WithValidAnswer());
        for (var i = 0; i < 5; i++)
            Assert.IsType<RequestOutcome.Created>(await service.RequestAsync(User, Request(), default));

        AssertDenied(await service.RequestAsync(User, Request(), default), RequestDenial.RateLimited);

        _time.Advance(TimeSpan.FromMinutes(1));
        Assert.IsType<RequestOutcome.Created>(await service.RequestAsync(User, Request(), default));
    }

    [Fact]
    public async Task Limit_applies_per_user()
    {
        var service = Service(CannedSuggestionProvider.WithValidAnswer());
        for (var i = 0; i < 5; i++) await service.RequestAsync(User, Request(), default);

        Assert.IsType<RequestOutcome.Created>(await service.RequestAsync(Guid.NewGuid(), Request(), default));
    }

    [Fact]
    public async Task Request_without_focus_never_reaches_the_provider()
    {
        var provider = CannedSuggestionProvider.WithValidAnswer();
        var request = new SuggestionRequest(new TrainingParameters(6, ExperienceLevel.Hobby, ExperienceSpread.Keine, 90, []), []);

        AssertDenied(await Service(provider).RequestAsync(User, request, default), RequestDenial.InvalidInput);
        Assert.Equal(0, provider.CallCount);
    }

    [Fact]
    public async Task Conflicting_suggestion_cannot_be_adopted()
    {
        var service = Service(CannedSuggestionProvider.Returning(Draft(minimum: 12)));
        var created = Assert.IsType<RequestOutcome.Created>(await service.RequestAsync(User, Request(participants: 6), default));

        var outcome = await service.AdoptAsync(User, created.Suggestion.Id, default);

        var blocked = Assert.IsType<AdoptOutcome.Blocked>(outcome);
        Assert.Contains(blocked.Findings, f => f.Code == FindingCodes.TooFewParticipants);
        Assert.Empty(_exercises.Items);
        Assert.Equal(SuggestionStatus.Pending, created.Suggestion.Status);
    }

    [Fact]
    public async Task Corrected_suggestion_can_be_adopted()
    {
        var service = Service(CannedSuggestionProvider.Returning(Draft(minimum: 12)));
        var created = Assert.IsType<RequestOutcome.Created>(await service.RequestAsync(User, Request(participants: 6), default));

        var findings = await service.EditAsync(User, created.Suggestion.Id, Draft(minimum: 4), default);
        var outcome = await service.AdoptAsync(User, created.Suggestion.Id, default);

        Assert.Empty(findings!);
        var adopted = Assert.IsType<AdoptOutcome.Adopted>(outcome);
        Assert.Equal(ExerciseOrigin.Ai, adopted.Exercise.Origin);
        Assert.Equal(adopted.Exercise.Id, created.Suggestion.AdoptedExerciseId);
        Assert.Single(_exercises.Items);
    }

    [Fact]
    public async Task Edit_that_breaks_a_valid_suggestion_blocks_adoption()
    {
        var service = Service(CannedSuggestionProvider.Returning(Draft()));
        var created = Assert.IsType<RequestOutcome.Created>(await service.RequestAsync(User, Request(), default));

        await service.EditAsync(User, created.Suggestion.Id, Draft() with { Title = "" }, default);

        Assert.IsType<AdoptOutcome.Blocked>(await service.AdoptAsync(User, created.Suggestion.Id, default));
    }

    [Fact]
    public async Task Discarded_suggestion_cannot_be_adopted()
    {
        var service = Service(CannedSuggestionProvider.Returning(Draft()));
        var created = Assert.IsType<RequestOutcome.Created>(await service.RequestAsync(User, Request(), default));

        Assert.True(await service.DiscardAsync(User, created.Suggestion.Id, default));

        Assert.IsType<AdoptOutcome.NotAvailable>(await service.AdoptAsync(User, created.Suggestion.Id, default));
        Assert.Empty(_exercises.Items);
    }

    [Fact]
    public async Task Suggestion_cannot_be_adopted_twice()
    {
        var service = Service(CannedSuggestionProvider.Returning(Draft()));
        var created = Assert.IsType<RequestOutcome.Created>(await service.RequestAsync(User, Request(), default));

        await service.AdoptAsync(User, created.Suggestion.Id, default);

        Assert.IsType<AdoptOutcome.NotAvailable>(await service.AdoptAsync(User, created.Suggestion.Id, default));
        Assert.Single(_exercises.Items);
    }

    [Fact]
    public async Task Suggestion_of_another_user_is_not_available()
    {
        var service = Service(CannedSuggestionProvider.Returning(Draft()));
        var created = Assert.IsType<RequestOutcome.Created>(await service.RequestAsync(User, Request(), default));

        Assert.IsType<AdoptOutcome.NotAvailable>(await service.AdoptAsync(Guid.NewGuid(), created.Suggestion.Id, default));
    }
}
