namespace CoachFlow.Domain;

/// <summary>
/// Anpassung oder Variante einer Übung. Eine Variante kann laut Entscheidung K4
/// auch eine andere Übung sein, dann verweist <see cref="ReferencedExerciseId"/> darauf.
/// </summary>
public sealed record ExerciseOption(
    OptionKind Kind,
    string? Title,
    string? Description,
    Guid? ReferencedExerciseId = null)
{
    public bool IsComplete =>
        !string.IsNullOrWhiteSpace(Title)
        && (!string.IsNullOrWhiteSpace(Description) || ReferencedExerciseId is not null);
}

/// <summary>
/// Inhalt einer Übung. Felder sind nullable, weil KI-Antworten unvollständig sein können
/// und solche Mängel als Befund sichtbar werden sollen, statt beim Einlesen zu verschwinden.
/// </summary>
public sealed record ExerciseContent(
    string? Title,
    string? Description,
    int? DurationMinutes,
    int? MinParticipants,
    int? MaxParticipants,
    IReadOnlyList<ExperienceLevel> Levels,
    IReadOnlyList<Guid> FocusAreaIds,
    IReadOnlyList<ExerciseOption> Options);
