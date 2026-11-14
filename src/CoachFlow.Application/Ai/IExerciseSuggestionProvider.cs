using CoachFlow.Domain;

namespace CoachFlow.Application.Ai;

/// <summary>Enthält nur Fachparameter, keine Nutzerdaten. Das Interface bleibt frei von Anbieterbegriffen.</summary>
public sealed record SuggestionRequest(TrainingParameters Parameters, IReadOnlyList<string> FocusAreaNames);

public enum AiFailureKind
{
    Unavailable,
    RateLimitedByProvider,
    InvalidResponse
}

public abstract record SuggestionResult
{
    /// <param name="Cost">Tatsächliche Kosten; null, wenn der Anbieter keine Verbrauchsdaten geliefert hat.</param>
    public sealed record Success(ExerciseContent Draft, decimal? Cost) : SuggestionResult;

    /// <param name="Cost">Tatsächliche Kosten; null, wenn nicht bekannt ist, ob der Aufruf berechnet wurde.</param>
    public sealed record Failure(AiFailureKind Kind, decimal? Cost) : SuggestionResult;
}

/// <summary>
/// Port zur externen KI. Anbieterwissen (Prompt, Modell, Antwortformat, Preise) gehört in den Adapter.
/// Adapter wandeln erwartbare Fehler in <see cref="SuggestionResult.Failure"/> um.
/// </summary>
public interface IExerciseSuggestionProvider
{
    string ProviderName { get; }

    /// <summary>Obergrenze der Kosten eines Aufrufs. Sie wird vor dem Aufruf reserviert.</summary>
    decimal MaxCostPerRequest { get; }

    Task<SuggestionResult> GenerateAsync(SuggestionRequest request, CancellationToken cancellationToken);
}
