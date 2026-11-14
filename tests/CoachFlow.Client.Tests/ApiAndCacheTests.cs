using System.Net;
using Microsoft.Extensions.Time.Testing;

namespace CoachFlow.Client.Tests;

public class CoachFlowApiTests
{
    [Fact]
    public async Task Network_error_becomes_offline_and_marks_state()
    {
        var (api, state) = Samples.Api(StubHandler.Throwing());

        var result = await api.ListPlansAsync();

        Assert.Equal(ApiStatus.Offline, result.Status);
        Assert.True(state.IsOffline);
    }

    [Fact]
    public async Task Server_error_without_coachflow_body_counts_as_offline()
    {
        var (api, state) = Samples.Api(StubHandler.Returning(HttpStatusCode.BadGateway, "<html>proxy</html>"));

        var result = await api.ListPlansAsync();

        Assert.Equal(ApiStatus.Offline, result.Status);
        Assert.Equal(ClientMessages.ServerUnavailable, result.Error!.Message);
        Assert.True(state.IsOffline);
    }

    [Fact]
    public async Task Server_error_with_coachflow_body_is_a_normal_failure()
    {
        var (api, state) = Samples.Api(StubHandler.Returning(HttpStatusCode.BadGateway, new ApiError("invalidResponse", "Ungültig.")));

        var result = await api.ListPlansAsync();

        Assert.Equal(ApiStatus.Failed, result.Status);
        Assert.Equal("invalidResponse", result.Error!.Code);
        Assert.False(state.IsOffline);
    }

    [Fact]
    public async Task Failed_login_does_not_reveal_whether_the_account_exists()
    {
        var (api, state) = Samples.Api(StubHandler.Returning(HttpStatusCode.Unauthorized, new { title = "Unauthorized", detail = "LockedOut" }));

        var result = await api.LoginAsync("a@example.org", "falsches-passwort");

        Assert.Equal(ApiStatus.Failed, result.Status);
        Assert.Equal(ClientMessages.LoginFailed, result.Error!.Message);
        Assert.Null(state.Account);
    }

    [Fact]
    public async Task Successful_login_sets_account()
    {
        var (api, state) = Samples.Api(StubHandler.Returning(HttpStatusCode.OK));

        await api.LoginAsync("a@example.org", "langes-passwort");

        Assert.Equal("a@example.org", state.Account);
        Assert.True(state.CanEdit);
    }

    [Fact]
    public async Task Identity_errors_are_shown_in_german()
    {
        var (api, _) = Samples.Api(StubHandler.Returning(HttpStatusCode.BadRequest, new
        {
            title = "One or more validation errors occurred.",
            errors = new Dictionary<string, string[]>
            {
                ["DuplicateUserName"] = ["Username 'a@example.org' is already taken."],
                ["PasswordTooShort"] = ["Passwords must be at least 10 characters."]
            }
        }));

        var result = await api.RegisterAsync("a@example.org", "kurz");

        Assert.Equal("registration-failed", result.Error!.Code);
        Assert.Contains("Für diese E-Mail-Adresse gibt es bereits ein Konto.", result.Error.Errors!);
        Assert.Contains("Das Passwort muss mindestens 10 Zeichen lang sein.", result.Error.Errors!);
    }

    [Fact]
    public async Task Plan_limit_warning_can_be_read_from_the_conflict()
    {
        var oldest = new Domain.StoredPlanInfo(Guid.NewGuid(), "Ältester", DateTimeOffset.UnixEpoch);
        var (api, _) = Samples.Api(StubHandler.Returning(HttpStatusCode.Conflict,
            new PlanLimitWarning("plan-limit-confirmation", "Warnung", oldest)));

        var result = await api.CreatePlanAsync(new PlanInput("Neu", Samples.Parameters, []), null);

        Assert.Equal(409, result.HttpStatus);
        Assert.Equal(oldest.Id, result.ReadBody<PlanLimitWarning>()!.PlanToDelete.Id);
    }
}

public class PlanLibraryTests
{
    private sealed class Switchable(Func<StubHandler> current) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            new HttpMessageInvoker(current()).SendAsync(request, cancellationToken);
    }

    [Fact]
    public async Task Plans_stay_readable_after_the_connection_is_lost()
    {
        var plans = new List<PlanDto> { Samples.Plan("Dienstag"), Samples.Plan("Donnerstag") };
        var online = true;
        var handler = new Switchable(() => online ? StubHandler.Returning(HttpStatusCode.OK, plans) : StubHandler.Throwing());
        var (api, state) = Samples.Api(handler);
        var time = new FakeTimeProvider(new DateTimeOffset(2026, 11, 8, 18, 0, 0, TimeSpan.Zero));
        var library = new PlanLibrary(api, new PlanCache(new MemoryStore(), time), state);
        state.SignedIn("a@example.org");

        Assert.IsType<PlanListResult.Online>(await library.LoadAsync());
        online = false;
        time.Advance(TimeSpan.FromHours(3));
        var result = Assert.IsType<PlanListResult.Cached>(await library.LoadAsync());

        Assert.Equal(["Dienstag", "Donnerstag"], result.Snapshot.Plans.Select(p => p.Title));
        Assert.Equal(new DateTimeOffset(2026, 11, 8, 18, 0, 0, TimeSpan.Zero), result.Snapshot.SavedAt);
        Assert.True(state.IsOffline);
        Assert.False(state.CanEdit);
        Assert.Equal(result.Snapshot.SavedAt, state.CachedAt);

        var (plan, fromCache, _) = await library.GetAsync(plans[1].Id);
        Assert.True(fromCache);
        Assert.Equal("Donnerstag", plan!.Title);
    }

    [Fact]
    public async Task Offline_without_local_copy_says_so()
    {
        var (api, state) = Samples.Api(StubHandler.Throwing());
        var library = new PlanLibrary(api, new PlanCache(new MemoryStore(), TimeProvider.System), state);

        var result = Assert.IsType<PlanListResult.Unavailable>(await library.LoadAsync());

        Assert.Equal(PlanLibrary.NoLocalCopy, result.Message);
    }

    [Fact]
    public async Task Coming_back_online_allows_editing_again()
    {
        var online = false;
        var handler = new Switchable(() => online ? StubHandler.Returning(HttpStatusCode.OK, new List<PlanDto>()) : StubHandler.Throwing());
        var (api, state) = Samples.Api(handler);
        var store = new MemoryStore();
        var cache = new PlanCache(store, TimeProvider.System);
        await cache.SaveAsync("a@example.org", [Samples.Plan()]);
        var library = new PlanLibrary(api, cache, state);

        await library.LoadAsync();
        Assert.Equal("a@example.org", state.Account);
        Assert.False(state.CanEdit);

        online = true;
        await library.LoadAsync();
        Assert.True(state.CanEdit);
        Assert.Null(state.CachedAt);
    }

    [Fact]
    public async Task Unreadable_local_copy_is_discarded()
    {
        var store = new MemoryStore();
        store.Values[PlanCache.Key] = "{kaputt";
        var cache = new PlanCache(store, TimeProvider.System);

        Assert.Null(await cache.LoadAsync());
        Assert.False(store.Values.ContainsKey(PlanCache.Key));
    }
}
