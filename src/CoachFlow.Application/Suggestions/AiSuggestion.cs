using CoachFlow.Domain;

namespace CoachFlow.Application.Suggestions;

public enum SuggestionStatus
{
    Pending,
    Adopted,
    Discarded
}

/// <summary>
/// Ein KI-Ergebnis ist eine eigene Stufe und noch keine Übung.
/// Eine Übung entsteht nur über die Übernahme in <see cref="SuggestionService"/>.
/// </summary>
public sealed class AiSuggestion
{
    public AiSuggestion(
        Guid id,
        Guid userId,
        TrainingParameters parameters,
        ExerciseContent draft,
        string providerName,
        DateTimeOffset createdAt)
    {
        Id = id;
        UserId = userId;
        Parameters = parameters;
        Draft = draft;
        ProviderName = providerName;
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public TrainingParameters Parameters { get; private set; }
    public ExerciseContent Draft { get; private set; }
    public string ProviderName { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public SuggestionStatus Status { get; private set; } = SuggestionStatus.Pending;
    public Guid? AdoptedExerciseId { get; private set; }
    public int Version { get; private set; } = 1;

    public IReadOnlyList<Finding> Findings => SuggestionValidator.Validate(Draft, Parameters);

    public void Edit(ExerciseContent draft)
    {
        EnsurePending();
        Draft = draft;
        Version++;
    }

    public void Discard()
    {
        EnsurePending();
        Status = SuggestionStatus.Discarded;
        Version++;
    }

    internal void MarkAdopted(Guid exerciseId)
    {
        EnsurePending();
        Status = SuggestionStatus.Adopted;
        AdoptedExerciseId = exerciseId;
        Version++;
    }

    private void EnsurePending()
    {
        if (Status != SuggestionStatus.Pending)
            throw new InvalidOperationException("Der Vorschlag wurde bereits abgeschlossen.");
    }
}

public interface ISuggestionRepository
{
    Task<AiSuggestion?> FindAsync(Guid id, CancellationToken cancellationToken);
    void Add(AiSuggestion suggestion);
}
