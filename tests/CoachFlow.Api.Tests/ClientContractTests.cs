using CoachFlow.Application.Exercises;
using CoachFlow.Client;
using CoachFlow.Domain;
using Microsoft.AspNetCore.Mvc.Testing;

namespace CoachFlow.Api.Tests;

/// <summary>
/// Der Code der Oberfläche (<see cref="CoachFlowApi"/>) gegen den echten Server: Passen DTOs und JSON zusammen?
/// </summary>
public sealed class ClientContractTests : IDisposable
{
    private static readonly Guid K1 = ExerciseService.SystemFocusAreas[1].Id;
    private readonly ApiFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    private (CoachFlowApi Api, AppState State) NewClient()
    {
        var http = _factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true, AllowAutoRedirect = false });
        var state = new AppState();
        return (new CoachFlowApi(http, state), state);
    }

    private static async Task<CoachFlowApi> SignedInAsync(CoachFlowApi api)
    {
        var email = $"{Guid.NewGuid():N}@example.org";
        Assert.True((await api.RegisterAsync(email, "langes-passwort-1")).IsOk);
        Assert.True((await api.LoginAsync(email, "langes-passwort-1")).IsOk);
        return api;
    }

    private static readonly TrainingParameters Parameters =
        new(8, ExperienceLevel.Hobby, ExperienceSpread.Keine, 90, [K1]);

    [Fact]
    public async Task Session_lifecycle_works_with_the_client()
    {
        var (api, state) = NewClient();
        Assert.Equal(ApiStatus.Unauthorized, (await api.GetAccountAsync()).Status);

        await SignedInAsync(api);
        var account = await api.GetAccountAsync();
        Assert.Equal(state.Account, account.Value!.Email);

        Assert.True((await api.LogoutAsync()).IsOk);
        Assert.Null(state.Account);
        Assert.Equal(ApiStatus.Unauthorized, (await api.ListPlansAsync()).Status);
    }

    [Fact]
    public async Task Duplicate_registration_and_wrong_password_give_german_messages()
    {
        var (api, _) = NewClient();
        var email = $"{Guid.NewGuid():N}@example.org";
        await api.RegisterAsync(email, "langes-passwort-1");

        var again = await api.RegisterAsync(email, "langes-passwort-1");
        Assert.Contains("Für diese E-Mail-Adresse gibt es bereits ein Konto.", again.Error!.Errors!);

        var wrong = await api.LoginAsync(email, "falsches-passwort");
        Assert.Equal(ClientMessages.LoginFailed, wrong.Error!.Message);
    }

    [Fact]
    public async Task Planning_round_trip_reads_into_client_types()
    {
        var (api, _) = NewClient();
        await SignedInAsync(api);

        var areas = (await api.ListFocusAreasAsync()).Value!;
        Assert.Contains(areas, a => a.Id == K1 && a.IsSystem);

        var model = new ExerciseModel { Title = "Pritschen", Description = "Paarweise", DurationMinutes = 20, MinParticipants = 2 };
        model.Levels.Add(ExperienceLevel.Hobby);
        model.Options.Add(new OptionModel { Kind = OptionKind.Anpassung, Title = "Leichter", Description = "Ball fangen" });
        var exercise = (await api.CreateExerciseAsync(model.ToContent())).Value!;
        Assert.Equal("Manual", exercise.Origin);

        var plan = new PlanModel { Title = "Dienstag" };
        plan.Parameters.FocusAreaIds.Add(K1);
        plan.Add(exercise.Id, exercise.Content);
        var created = await api.CreatePlanAsync(plan.ToInput(), null);
        Assert.True(created.IsOk, created.Error?.Message);

        var stored = (await api.ListPlansAsync()).Value!.Single();
        Assert.Equal(20, stored.TotalMinutes);
        Assert.Equal(plan.Findings.Select(f => f.Code), stored.Findings.Select(f => f.Code)); // Client und Server prüfen gleich
        Assert.Equal("Leichter", stored.Items[0].Exercise.Options[0].Title);

        var edited = PlanModel.From(stored);
        edited.Items[0].PlannedMinutes = 30;
        var updated = await api.UpdatePlanAsync(stored.Id, stored.Version, edited.ToInput());
        Assert.Equal(30, updated.Value!.TotalMinutes);

        var stale = await api.UpdatePlanAsync(stored.Id, stored.Version, edited.ToInput());
        Assert.Equal("version-conflict", stale.Error!.Code);
    }

    [Fact]
    public async Task Eleventh_plan_warning_is_readable_and_confirmable()
    {
        var (api, _) = NewClient();
        await SignedInAsync(api);
        for (var i = 1; i <= PlanLimitPolicy.MaxPlansPerUser; i++)
            Assert.True((await api.CreatePlanAsync(new PlanInput($"Plan {i}", Parameters, []), null)).IsOk);

        var refused = await api.CreatePlanAsync(new PlanInput("Plan 11", Parameters, []), null);
        var warning = refused.ReadBody<PlanLimitWarning>()!;
        Assert.Equal("plan-limit-confirmation", warning.Code);
        Assert.Equal("Plan 1", warning.PlanToDelete.Title);

        Assert.True((await api.CreatePlanAsync(new PlanInput("Plan 11", Parameters, []), warning.PlanToDelete.Id)).IsOk);
        var titles = (await api.ListPlansAsync()).Value!.Select(p => p.Title).ToList();
        Assert.Equal(PlanLimitPolicy.MaxPlansPerUser, titles.Count);
        Assert.DoesNotContain("Plan 1", titles);
    }

    [Fact]
    public async Task Suggestion_flow_through_the_requester()
    {
        var (api, _) = NewClient();
        await SignedInAsync(api);
        var requester = new SuggestionRequester(api, TimeProvider.System, SuggestionTiming.Default);

        var attempt = await requester.RequestAsync(Parameters, () => { }, CancellationToken.None);
        var suggestion = Assert.IsType<SuggestionAttempt.Created>(attempt).Suggestion;
        Assert.Equal("Pending", suggestion.Status);
        Assert.DoesNotContain(SuggestionValidator.Validate(suggestion.Draft, suggestion.Parameters), f => f.Severity == FindingSeverity.Blocker);

        // Bearbeiten im Formular und Übernehmen, wie es SuggestionPanel tut.
        var draft = ExerciseModel.From(suggestion.Draft);
        draft.Title = "Angepasster Vorschlag";
        Assert.True((await api.EditSuggestionAsync(suggestion.Id, draft.ToContent())).IsOk);
        var adopted = await api.AdoptSuggestionAsync(suggestion.Id);
        Assert.Equal("Angepasster Vorschlag", adopted.Value!.Content.Title);
        Assert.Equal("Ai", adopted.Value.Origin);

        Assert.Equal(404, (await api.AdoptSuggestionAsync(suggestion.Id)).HttpStatus);
    }

    [Fact]
    public async Task Blocked_adoption_returns_findings_to_the_client()
    {
        var (api, _) = NewClient();
        await SignedInAsync(api);
        var suggestion = (await api.RequestSuggestionAsync(Parameters)).Value!;

        var broken = ExerciseModel.From(suggestion.Draft);
        broken.DurationMinutes = 500; // länger als das Training: Blocker
        await api.EditSuggestionAsync(suggestion.Id, broken.ToContent());

        var result = await api.AdoptSuggestionAsync(suggestion.Id);
        Assert.Equal(422, result.HttpStatus);
        Assert.Contains(result.ReadBody<AdoptBlocked>()!.Findings, f => f.Code == FindingCodes.DurationExceedsSession);
    }
}
