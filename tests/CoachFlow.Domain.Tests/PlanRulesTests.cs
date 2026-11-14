using CoachFlow.Domain;

namespace CoachFlow.Domain.Tests;

public class PlanRulesTests
{
    private static TrainingParameters Parameters(
        int participants = 8,
        ExperienceSpread spread = ExperienceSpread.Keine,
        int duration = 90) =>
        new(participants, ExperienceLevel.Hobby, spread, duration, []);

    private static PlanEntry Entry(int minutes = 30, int? min = 4, int? max = null, params ExerciseOption[] options) =>
        new("Übung", minutes, min, max, options);

    [Fact]
    public void Total_duration_adds_all_entries() =>
        Assert.Equal(75, PlanRules.TotalMinutes([Entry(30), Entry(45)]));

    [Fact]
    public void Empty_plan_has_zero_minutes() =>
        Assert.Equal(0, PlanRules.TotalMinutes([]));

    [Fact]
    public void Remaining_time_is_negative_when_exceeded() =>
        Assert.Equal(-10, PlanRules.RemainingMinutes(Parameters(duration: 60), [Entry(70)]));

    [Fact]
    public void Exact_duration_gives_no_finding() =>
        Assert.Empty(PlanRules.Check(Parameters(duration: 60), [Entry(30), Entry(30)]));

    [Fact]
    public void Exceeded_duration_is_a_warning()
    {
        var finding = Assert.Single(PlanRules.Check(Parameters(duration: 60), [Entry(61)]));

        Assert.Equal(FindingCodes.PlanDurationExceeded, finding.Code);
        Assert.Equal(FindingSeverity.Warning, finding.Severity);
    }

    [Fact]
    public void Too_few_participants_is_a_warning_so_drafts_stay_saveable()
    {
        var finding = Assert.Single(PlanRules.Check(Parameters(participants: 6), [Entry(min: 12)]));

        Assert.Equal(FindingCodes.TooFewParticipants, finding.Code);
        Assert.Equal(FindingSeverity.Warning, finding.Severity);
    }

    [Theory]
    [InlineData(ExperienceSpread.Anpassungen, FindingCodes.AdaptationMissing)]
    [InlineData(ExperienceSpread.Varianten, FindingCodes.VariantMissing)]
    public void Missing_option_for_spread_is_reported(ExperienceSpread spread, string expected) =>
        Assert.Equal(expected, Assert.Single(PlanRules.Check(Parameters(spread: spread), [Entry()])).Code);

    [Theory]
    [InlineData(4, true)]
    [InlineData(5, false)]
    [InlineData(14, false)]
    [InlineData(15, true)]
    public void Five_to_fourteen_is_typical_and_not_binding(int participants, bool expectWarning)
    {
        var findings = PlanRules.Check(Parameters(participants: participants), []);

        Assert.Equal(expectWarning, findings.Any(f => f.Code == FindingCodes.ParticipantCountUntypical));
        Assert.Empty(Parameters(participants: participants).GetErrors(forAiRequest: false));
    }

    [Fact]
    public void Plan_allows_no_focus_but_ai_requires_one()
    {
        var parameters = Parameters();

        Assert.Empty(parameters.GetErrors(forAiRequest: false));
        Assert.NotEmpty(parameters.GetErrors(forAiRequest: true));
    }

    [Fact]
    public void Three_focus_areas_are_rejected()
    {
        var parameters = Parameters() with { FocusAreaIds = [Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()] };

        Assert.NotEmpty(parameters.GetErrors(forAiRequest: false));
    }
}

public class PlanLimitPolicyTests
{
    private static readonly DateTimeOffset Start = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static List<StoredPlanInfo> Plans(int count) =>
        Enumerable.Range(0, count)
            .Select(i => new StoredPlanInfo(Guid.NewGuid(), $"Plan {i}", Start.AddDays(i)))
            .ToList();

    [Fact]
    public void Ninth_to_tenth_plan_is_allowed() =>
        Assert.IsType<PlanLimitDecision.Allowed>(PlanLimitPolicy.Evaluate(Plans(9)));

    [Fact]
    public void Eleventh_plan_requires_confirmation_for_the_oldest()
    {
        var plans = Plans(10);
        plans.Reverse();

        var decision = Assert.IsType<PlanLimitDecision.ConfirmationRequired>(PlanLimitPolicy.Evaluate(plans));

        Assert.Equal("Plan 0", decision.PlanToDelete.Title);
    }
}
