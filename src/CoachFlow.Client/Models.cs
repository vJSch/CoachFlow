using CoachFlow.Domain;

namespace CoachFlow.Client;

// Antwortformen der API. Fachliche Bausteine (Parameter, Übungsinhalt, Befunde) kommen aus der Domäne,
// damit Client und Server dieselben Regeln verwenden.

public sealed record PlanItemDto(Guid? SourceExerciseId, ExerciseContent Exercise, int PlannedMinutes, string? Note)
{
    public PlanEntry ToEntry() => new(
        Exercise.Title ?? "Ohne Titel",
        PlannedMinutes,
        Exercise.MinParticipants,
        Exercise.MaxParticipants,
        Exercise.Options);
}

public sealed record PlanDto(
    Guid Id,
    string Title,
    TrainingParameters Parameters,
    IReadOnlyList<PlanItemDto> Items,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    int Version,
    int TotalMinutes,
    IReadOnlyList<Finding> Findings);

public sealed record ExerciseDto(
    Guid Id,
    ExerciseContent Content,
    string Origin,
    Guid CreatedBy,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    int Version);

public sealed record FocusAreaDto(Guid Id, string Name, bool IsSystem);

public sealed record SuggestionDto(
    Guid Id,
    TrainingParameters Parameters,
    ExerciseContent Draft,
    string ProviderName,
    string Status,
    int Version,
    IReadOnlyList<Finding> Findings);

public sealed record AccountInfo(string Email);

public sealed record ApiError(string Code, string Message, IReadOnlyList<string>? Errors = null);

public sealed record PlanLimitWarning(string Code, string Message, StoredPlanInfo PlanToDelete);

public sealed record AdoptBlocked(string Code, string Message, IReadOnlyList<Finding> Findings);

public sealed record PlanInput(string Title, TrainingParameters Parameters, IReadOnlyList<PlanItemDto> Items);
