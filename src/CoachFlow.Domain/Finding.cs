namespace CoachFlow.Domain;

public sealed record Finding(string Code, FindingSeverity Severity, string Message);

public static class FindingCodes
{
    public const string TitleMissing = "title.missing";
    public const string DescriptionMissing = "description.missing";
    public const string DurationMissing = "duration.missing";
    public const string DurationExceedsSession = "duration.exceeds-session";
    public const string PlanDurationExceeded = "plan.duration-exceeded";
    public const string MinParticipantsMissing = "participants.min-missing";
    public const string ParticipantRangeInvalid = "participants.range-invalid";
    public const string TooFewParticipants = "participants.too-few";
    public const string TooManyParticipants = "participants.too-many";
    public const string ParticipantCountUntypical = "participants.untypical";
    public const string AdaptationMissing = "spread.adaptation-missing";
    public const string VariantMissing = "spread.variant-missing";
    public const string OptionIncomplete = "option.incomplete";
    public const string TooManyFocusAreas = "focus.too-many";
    public const string FocusMismatch = "focus.mismatch";
    public const string LevelMismatch = "level.mismatch";
}
