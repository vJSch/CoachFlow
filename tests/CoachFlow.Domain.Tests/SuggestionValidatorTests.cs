using CoachFlow.Domain;

namespace CoachFlow.Domain.Tests;

public class SuggestionValidatorTests
{
    private static readonly Guid Focus = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private static TrainingParameters Parameters(
        int participants = 6,
        ExperienceSpread spread = ExperienceSpread.Keine,
        int duration = 90) =>
        new(participants, ExperienceLevel.Hobby, spread, duration, [Focus]);

    private static ExerciseContent ValidDraft() => new(
        "Annahme", "Beschreibung", 15, 4, 8, [ExperienceLevel.Hobby], [Focus], []);

    private static IEnumerable<string> Blockers(ExerciseContent draft, TrainingParameters parameters) =>
        SuggestionValidator.Validate(draft, parameters)
            .Where(f => f.Severity == FindingSeverity.Blocker)
            .Select(f => f.Code);

    [Fact]
    public void Valid_draft_has_no_findings() =>
        Assert.Empty(SuggestionValidator.Validate(ValidDraft(), Parameters()));

    [Fact]
    public void Twelve_required_with_six_available_is_blocked()
    {
        var findings = SuggestionValidator.Validate(ValidDraft() with { MinParticipants = 12, MaxParticipants = 14 }, Parameters());

        var finding = Assert.Single(findings, f => f.Code == FindingCodes.TooFewParticipants);
        Assert.Equal(FindingSeverity.Blocker, finding.Severity);
        Assert.Contains("12", finding.Message);
        Assert.Contains("6", finding.Message);
    }

    [Theory]
    [InlineData(null, "Beschreibung", 15, 4, FindingCodes.TitleMissing)]
    [InlineData("   ", "Beschreibung", 15, 4, FindingCodes.TitleMissing)]
    [InlineData("Titel", null, 15, 4, FindingCodes.DescriptionMissing)]
    [InlineData("Titel", "Beschreibung", null, 4, FindingCodes.DurationMissing)]
    [InlineData("Titel", "Beschreibung", 0, 4, FindingCodes.DurationMissing)]
    [InlineData("Titel", "Beschreibung", 15, null, FindingCodes.MinParticipantsMissing)]
    [InlineData("Titel", "Beschreibung", 15, 0, FindingCodes.MinParticipantsMissing)]
    public void Missing_required_field_is_blocked(string? title, string? description, int? duration, int? minimum, string expected)
    {
        var draft = ValidDraft() with
        {
            Title = title,
            Description = description,
            DurationMinutes = duration,
            MinParticipants = minimum
        };

        Assert.Contains(expected, Blockers(draft, Parameters()));
    }

    [Fact]
    public void Maximum_below_minimum_is_blocked() =>
        Assert.Contains(FindingCodes.ParticipantRangeInvalid,
            Blockers(ValidDraft() with { MinParticipants = 5, MaxParticipants = 4 }, Parameters()));

    [Fact]
    public void Exercise_longer_than_session_is_blocked() =>
        Assert.Contains(FindingCodes.DurationExceedsSession,
            Blockers(ValidDraft() with { DurationMinutes = 91 }, Parameters(duration: 90)));

    [Fact]
    public void Exercise_as_long_as_session_is_allowed() =>
        Assert.Empty(Blockers(ValidDraft() with { DurationMinutes = 90 }, Parameters(duration: 90)));

    [Fact]
    public void More_participants_than_maximum_is_only_a_warning()
    {
        var finding = Assert.Single(SuggestionValidator.Validate(ValidDraft() with { MaxParticipants = 5 }, Parameters()));

        Assert.Equal(FindingCodes.TooManyParticipants, finding.Code);
        Assert.Equal(FindingSeverity.Warning, finding.Severity);
    }

    [Fact]
    public void Spread_two_requires_adaptation() =>
        Assert.Contains(FindingCodes.AdaptationMissing, Blockers(ValidDraft(), Parameters(spread: ExperienceSpread.Anpassungen)));

    [Fact]
    public void Spread_three_requires_variant() =>
        Assert.Contains(FindingCodes.VariantMissing, Blockers(ValidDraft(), Parameters(spread: ExperienceSpread.Varianten)));

    [Fact]
    public void Adaptation_does_not_satisfy_spread_three()
    {
        var draft = ValidDraft() with { Options = [new ExerciseOption(OptionKind.Anpassung, "Leichter", "Zuwerfen")] };

        Assert.Contains(FindingCodes.VariantMissing, Blockers(draft, Parameters(spread: ExperienceSpread.Varianten)));
    }

    [Fact]
    public void Variant_may_reference_another_exercise()
    {
        var draft = ValidDraft() with { Options = [new ExerciseOption(OptionKind.Variante, "Andere Übung", null, Guid.NewGuid())] };

        Assert.Empty(Blockers(draft, Parameters(spread: ExperienceSpread.Varianten)));
    }

    [Fact]
    public void Incomplete_option_is_blocked_and_does_not_count()
    {
        var draft = ValidDraft() with { Options = [new ExerciseOption(OptionKind.Variante, "Nur Titel", null)] };

        var blockers = Blockers(draft, Parameters(spread: ExperienceSpread.Varianten)).ToList();

        Assert.Contains(FindingCodes.OptionIncomplete, blockers);
        Assert.Contains(FindingCodes.VariantMissing, blockers);
    }

    [Fact]
    public void Other_level_and_focus_are_warnings()
    {
        var draft = ValidDraft() with { Levels = [ExperienceLevel.JugendKleinfeld], FocusAreaIds = [Guid.NewGuid()] };

        var findings = SuggestionValidator.Validate(draft, Parameters());

        Assert.Equal(2, findings.Count);
        Assert.All(findings, f => Assert.Equal(FindingSeverity.Warning, f.Severity));
        Assert.False(SuggestionValidator.HasBlockers(findings));
    }

    [Fact]
    public void Same_input_gives_same_result()
    {
        var draft = new ExerciseContent(null, null, null, null, null, [], [], []);

        Assert.Equal(SuggestionValidator.Validate(draft, Parameters()), SuggestionValidator.Validate(draft, Parameters()));
    }
}
