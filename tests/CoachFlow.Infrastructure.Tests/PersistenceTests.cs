using CoachFlow.Application;
using CoachFlow.Application.Ai;
using CoachFlow.Application.Exercises;
using CoachFlow.Application.Plans;
using CoachFlow.Application.Suggestions;
using CoachFlow.Domain;
using CoachFlow.Infrastructure.Budget;
using Microsoft.EntityFrameworkCore;

namespace CoachFlow.Infrastructure.Tests;

public sealed class PlanPersistenceTests : IDisposable
{
    private static readonly Guid User = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private readonly Database _db = new();

    public void Dispose() => _db.Dispose();

    private static PlanInput Input(string title = "Dienstag", int participants = 8, int itemMinimum = 4) => new(
        title,
        new TrainingParameters(participants, ExperienceLevel.Hobby, ExperienceSpread.Varianten, 90, [ExerciseService.SystemFocusAreas[1].Id]),
        [
            new PlanItem(
                Guid.NewGuid(),
                new ExerciseContent("Annahme", "Zu dritt", 20, itemMinimum, null, [ExperienceLevel.Hobby], [],
                    [new ExerciseOption(OptionKind.Variante, "Leichter", "Zuwerfen"),
                     new ExerciseOption(OptionKind.Variante, "Andere Übung", null, Guid.NewGuid())]),
                20,
                "Auf Schrittfolge achten")
        ]);

    private async Task<TrainingPlan> CreateAsync(string title = "Dienstag")
    {
        await using var db = _db.Context();
        var outcome = await _db.Plans(db).CreateAsync(User, Input(title), null, default);
        return Assert.IsType<CreatePlanOutcome.Created>(outcome).Plan;
    }

    [Fact]
    public async Task Plan_with_snapshot_and_variants_survives_a_round_trip()
    {
        var created = await CreateAsync();

        await using var db = _db.Context();
        var loaded = await _db.Plans(db).GetAsync(User, created.Id, default);

        Assert.NotNull(loaded);
        Assert.Equal(created.Parameters.FocusAreaIds, loaded.Parameters.FocusAreaIds);
        Assert.Equal(ExperienceSpread.Varianten, loaded.Parameters.Spread);
        var item = Assert.Single(loaded.Items);
        Assert.Equal("Auf Schrittfolge achten", item.Note);
        Assert.Equal(created.Items[0].Exercise.Options, item.Exercise.Options);
        Assert.Equal(20, loaded.TotalMinutes);
    }

    [Fact]
    public async Task Enums_are_stored_as_readable_text()
    {
        await CreateAsync();

        await using var db = _db.Context();
        var raw = await db.Database.SqlQueryRaw<string>("SELECT \"Items\" AS \"Value\" FROM \"Plans\"").SingleAsync();

        Assert.Contains("\"Variante\"", raw);
    }

    [Fact]
    public async Task Eleventh_plan_deletes_the_oldest_only_after_confirmation()
    {
        var first = await CreateAsync("Plan 1");
        for (var i = 2; i <= 10; i++)
        {
            _db.Time.Advance(TimeSpan.FromMinutes(1));
            await CreateAsync($"Plan {i}");
        }

        await using (var db = _db.Context())
        {
            var warning = Assert.IsType<CreatePlanOutcome.ConfirmationRequired>(
                await _db.Plans(db).CreateAsync(User, Input("Plan 11"), null, default));
            Assert.Equal(first.Id, warning.PlanToDelete.Id);
            Assert.Contains("Plan 1", warning.Message);

            Assert.IsType<CreatePlanOutcome.ConfirmationRequired>(
                await _db.Plans(db).CreateAsync(User, Input("Plan 11"), Guid.NewGuid(), default));
        }

        await using (var db = _db.Context())
        {
            Assert.Equal(10, (await _db.Plans(db).ListAsync(User, default)).Count);
            var created = Assert.IsType<CreatePlanOutcome.Created>(
                await _db.Plans(db).CreateAsync(User, Input("Plan 11"), first.Id, default));
            Assert.Equal(first.Id, created.DeletedPlan!.Id);
        }

        await using (var db = _db.Context())
        {
            var titles = (await _db.Plans(db).ListAsync(User, default)).Select(p => p.Title).ToList();
            Assert.Equal(10, titles.Count);
            Assert.DoesNotContain("Plan 1", titles);
            Assert.Contains("Plan 11", titles);
        }
    }

    [Fact]
    public async Task Limit_counts_per_user()
    {
        for (var i = 0; i < 10; i++) await CreateAsync($"Plan {i}");

        await using var db = _db.Context();
        Assert.IsType<CreatePlanOutcome.Created>(await _db.Plans(db).CreateAsync(Guid.NewGuid(), Input(), null, default));
    }

    [Fact]
    public async Task Parallel_edit_of_the_same_plan_is_detected()
    {
        var plan = await CreateAsync();
        await using var first = _db.Context();
        await using var second = _db.Context();
        var firstService = _db.Plans(first);
        var secondService = _db.Plans(second);
        await firstService.GetAsync(User, plan.Id, default);
        await secondService.GetAsync(User, plan.Id, default);

        var saved = await firstService.UpdateAsync(User, plan.Id, 1, Input("Zuerst"), default);
        var conflict = await secondService.UpdateAsync(User, plan.Id, 1, Input("Danach"), default);

        Assert.IsType<ChangeOutcome<TrainingPlan>.Done>(saved);
        Assert.IsType<ChangeOutcome<TrainingPlan>.Conflict>(conflict);
        await using var check = _db.Context();
        var stored = await _db.Plans(check).GetAsync(User, plan.Id, default);
        Assert.Equal("Zuerst", stored!.Title);
        Assert.Equal(2, stored.Version);
    }

    [Fact]
    public async Task Plan_with_warnings_is_still_saved()
    {
        await using var db = _db.Context();
        var outcome = await _db.Plans(db).CreateAsync(User, Input(participants: 6, itemMinimum: 12), null, default);

        var plan = Assert.IsType<CreatePlanOutcome.Created>(outcome).Plan;
        Assert.Contains(plan.Findings, f => f.Code == FindingCodes.TooFewParticipants);
    }

    [Fact]
    public async Task Other_users_cannot_read_or_delete_a_plan()
    {
        var plan = await CreateAsync();
        var stranger = Guid.NewGuid();

        await using var db = _db.Context();
        Assert.Null(await _db.Plans(db).GetAsync(stranger, plan.Id, default));
        Assert.False(await _db.Plans(db).DeleteAsync(stranger, plan.Id, default));
        Assert.NotNull(await _db.Plans(db).GetAsync(User, plan.Id, default));
    }
}

public sealed class ExercisePersistenceTests : IDisposable
{
    private static readonly Guid User = Guid.NewGuid();
    private readonly Database _db = new();

    public void Dispose() => _db.Dispose();

    private static ExerciseContent Content(string title = "Pritschen") =>
        new(title, null, 10, 2, 12, [ExperienceLevel.JugendKleinfeld], [ExerciseService.SystemFocusAreas[0].Id], []);

    private async Task<Exercise> CreateAsync()
    {
        await using var db = _db.Context();
        return Assert.IsType<ChangeOutcome<Exercise>.Done>(await _db.Exercises(db).CreateAsync(User, Content(), default)).Value;
    }

    [Fact]
    public async Task Exercise_can_be_created_changed_and_archived()
    {
        var exercise = await CreateAsync();

        await using (var db = _db.Context())
            Assert.IsType<ChangeOutcome<Exercise>.Done>(await _db.Exercises(db).UpdateAsync(exercise.Id, 1, Content("Baggern"), default));

        await using (var db = _db.Context())
        {
            var listed = Assert.Single(await _db.Exercises(db).ListAsync(default));
            Assert.Equal("Baggern", listed.Content.Title);
            Assert.IsType<ChangeOutcome<Exercise>.Done>(await _db.Exercises(db).ArchiveAsync(exercise.Id, 2, default));
        }

        await using (var db = _db.Context())
            Assert.Empty(await _db.Exercises(db).ListAsync(default));
    }

    [Fact]
    public async Task Parallel_edit_in_the_shared_pool_is_detected()
    {
        var exercise = await CreateAsync();
        await using var first = _db.Context();
        await using var second = _db.Context();
        var firstService = _db.Exercises(first);
        var secondService = _db.Exercises(second);
        await new EfExerciseRepository(first).FindAsync(exercise.Id, default);
        await new EfExerciseRepository(second).FindAsync(exercise.Id, default);

        Assert.IsType<ChangeOutcome<Exercise>.Done>(await firstService.UpdateAsync(exercise.Id, 1, Content("A"), default));
        Assert.IsType<ChangeOutcome<Exercise>.Conflict>(await secondService.UpdateAsync(exercise.Id, 1, Content("B"), default));
    }

    [Fact]
    public async Task Stale_version_from_the_client_is_rejected()
    {
        var exercise = await CreateAsync();
        await using (var db = _db.Context())
            await _db.Exercises(db).UpdateAsync(exercise.Id, 1, Content("A"), default);

        await using var later = _db.Context();
        Assert.IsType<ChangeOutcome<Exercise>.Conflict>(await _db.Exercises(later).UpdateAsync(exercise.Id, 1, Content("B"), default));
    }

    [Fact]
    public async Task Invalid_exercise_and_unknown_focus_are_rejected()
    {
        await using var db = _db.Context();
        var service = _db.Exercises(db);

        Assert.IsType<ChangeOutcome<Exercise>.Invalid>(await service.CreateAsync(User, Content() with { Title = " " }, default));
        Assert.IsType<ChangeOutcome<Exercise>.Invalid>(await service.CreateAsync(User, Content() with { FocusAreaIds = [Guid.NewGuid()] }, default));
    }

    [Fact]
    public async Task Custom_focus_area_is_added_once_and_listed_after_system_areas()
    {
        await using (var db = _db.Context())
        {
            var service = _db.Exercises(db);
            var added = Assert.IsType<ChangeOutcome<FocusArea>.Done>(await service.AddFocusAreaAsync(" Aufschlag-Annahme ", default));
            var again = Assert.IsType<ChangeOutcome<FocusArea>.Done>(await service.AddFocusAreaAsync("aufschlag-annahme", default));
            Assert.Equal(added.Value.Id, again.Value.Id);
            Assert.IsType<ChangeOutcome<FocusArea>.Done>(await service.AddFocusAreaAsync("k1", default));
        }

        await using var check = _db.Context();
        var names = (await _db.Exercises(check).ListFocusAreasAsync(default)).Select(f => f.Name).ToList();
        Assert.Equal(["K0", "K1", "K2", "Abwehr", "Mannschaftstaktik", "Einzeltaktik", "Aufschlag-Annahme"], names);
    }
}

public sealed class SuggestionPersistenceTests : IDisposable
{
    private static readonly Guid User = Guid.NewGuid();
    private readonly Database _db = new();

    public void Dispose() => _db.Dispose();

    private static SuggestionRequest Request() => new(
        new TrainingParameters(8, ExperienceLevel.Hobby, ExperienceSpread.Varianten, 90, [ExerciseService.SystemFocusAreas[1].Id]),
        ["K1"]);

    private async Task<Guid> RequestAsync()
    {
        await using var db = _db.Context();
        var outcome = await _db.Suggestions(db, Database.ValidProvider()).RequestAsync(User, Request(), default);
        return Assert.IsType<RequestOutcome.Created>(outcome).Suggestion.Id;
    }

    [Fact]
    public async Task Suggestion_is_stored_and_adopted_in_a_later_request()
    {
        var id = await RequestAsync();

        await using (var db = _db.Context())
        {
            var adopted = Assert.IsType<AdoptOutcome.Adopted>(await _db.Suggestions(db, Database.ValidProvider()).AdoptAsync(User, id, default));
            Assert.Equal(ExerciseOrigin.Ai, adopted.Exercise.Origin);
        }

        await using var check = _db.Context();
        Assert.Equal(ExerciseOrigin.Ai, (await check.Exercises.SingleAsync()).Origin);
        Assert.Equal(SuggestionStatus.Adopted, (await check.Suggestions.SingleAsync()).Status);
    }

    [Fact]
    public async Task Double_click_on_adopt_creates_exactly_one_exercise()
    {
        var id = await RequestAsync();
        await using var first = _db.Context();
        await using var second = _db.Context();
        var firstService = _db.Suggestions(first, Database.ValidProvider());
        var secondService = _db.Suggestions(second, Database.ValidProvider());
        await new EfSuggestionRepository(first).FindAsync(id, default);
        await new EfSuggestionRepository(second).FindAsync(id, default);

        var outcomes = new[]
        {
            await firstService.AdoptAsync(User, id, default),
            await secondService.AdoptAsync(User, id, default)
        };

        Assert.Single(outcomes.OfType<AdoptOutcome.Adopted>());
        Assert.Single(outcomes.OfType<AdoptOutcome.NotAvailable>());
        await using var check = _db.Context();
        Assert.Equal(1, await check.Exercises.CountAsync());
    }
}

public sealed class BudgetLedgerTests : IDisposable
{
    private readonly Database _db = new();

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task Settled_cost_replaces_the_reservation()
    {
        await using var db = _db.Context();
        var ledger = new EfAiBudgetLedger(db, _db.Time);

        var id = await ledger.TryReserveAsync("2026-11", 0.10m, 50m, default);
        await ledger.SettleAsync(id!.Value, 0.0234m, default);
        await ledger.SettleAsync(id.Value, 9m, default);

        var summary = await ledger.GetSummaryAsync("2026-11", default);
        Assert.Equal(0.0234m, summary.Used);
        Assert.Equal(1, summary.Requests);
        Assert.Equal(0, summary.RequestsWithUnknownCost);
    }

    [Fact]
    public async Task Unsettled_reservation_counts_in_full()
    {
        await using var db = _db.Context();
        var ledger = new EfAiBudgetLedger(db, _db.Time);

        await ledger.TryReserveAsync("2026-11", 0.10m, 50m, default);

        var summary = await ledger.GetSummaryAsync("2026-11", default);
        Assert.Equal(0.10m, summary.Used);
        Assert.Equal(1, summary.RequestsWithUnknownCost);
    }

    [Fact]
    public async Task Exact_budget_is_usable_but_not_exceeded()
    {
        await using var db = _db.Context();
        var ledger = new EfAiBudgetLedger(db, _db.Time);

        Assert.NotNull(await ledger.TryReserveAsync("2026-11", 49.99m, 50m, default));
        Assert.NotNull(await ledger.TryReserveAsync("2026-11", 0.01m, 50m, default));
        Assert.Null(await ledger.TryReserveAsync("2026-11", 0.000001m, 50m, default));
    }

    [Fact]
    public async Task New_month_starts_with_full_budget()
    {
        await using var db = _db.Context();
        var ledger = new EfAiBudgetLedger(db, _db.Time);
        await ledger.TryReserveAsync("2026-11", 50m, 50m, default);

        Assert.NotNull(await ledger.TryReserveAsync("2026-12", 50m, 50m, default));
    }

    [Fact]
    public async Task Parallel_reservations_never_exceed_the_budget()
    {
        const int attempts = 20;
        var results = await Task.WhenAll(Enumerable.Range(0, attempts).Select(_ => Task.Run(async () =>
        {
            await using var db = _db.Context();
            return await new EfAiBudgetLedger(db, _db.Time).TryReserveAsync("2026-11", 10m, 50m, default);
        })));

        Assert.Equal(5, results.Count(r => r is not null));
        await using var check = _db.Context();
        var summary = await new EfAiBudgetLedger(check, _db.Time).GetSummaryAsync("2026-11", default);
        Assert.Equal(50m, summary.Used);
        Assert.Equal(5, summary.Requests);
    }
}
