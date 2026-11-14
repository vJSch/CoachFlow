using CoachFlow.Application.Ai;
using CoachFlow.Domain;

namespace CoachFlow.AiAdapters.Canned;

/// <summary>
/// Testadapter mit vorbereiteten Antworten (Entscheidung K1). Er ruft keine externe API auf
/// und liefert bei gleichen Eingaben immer dasselbe Ergebnis.
/// </summary>
public sealed class CannedSuggestionProvider : IExerciseSuggestionProvider
{
    private readonly Func<SuggestionRequest, CancellationToken, Task<SuggestionResult>> _respond;

    private CannedSuggestionProvider(Func<SuggestionRequest, CancellationToken, Task<SuggestionResult>> respond) =>
        _respond = respond;

    public string ProviderName => "canned";

    public decimal MaxCostPerRequest { get; init; } = 0.10m;

    public int CallCount { get; private set; }

    /// <summary>Liefert eine gültige Übung, die zu den Anfrageparametern passt.</summary>
    public static CannedSuggestionProvider WithValidAnswer() =>
        new((request, _) => Task.FromResult<SuggestionResult>(
            new SuggestionResult.Success(BuildValidDraft(request.Parameters), 0.02m)));

    public static CannedSuggestionProvider Returning(ExerciseContent draft, decimal? cost = 0.02m) =>
        new((_, _) => Task.FromResult<SuggestionResult>(new SuggestionResult.Success(draft, cost)));

    public static CannedSuggestionProvider Failing(AiFailureKind kind, decimal? cost = null) =>
        new((_, _) => Task.FromResult<SuggestionResult>(new SuggestionResult.Failure(kind, cost)));

    public static CannedSuggestionProvider Throwing() =>
        new((_, _) => throw new HttpRequestException("Simulierter Verbindungsfehler"));

    /// <summary>Antwortet nie, reagiert aber auf den Abbruch.</summary>
    public static CannedSuggestionProvider Hanging() =>
        new(async (_, cancellationToken) =>
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
            throw new InvalidOperationException("Nicht erreichbar");
        });

    /// <summary>Antwortet nie und ignoriert den Abbruch, wie ein fehlerhafter Adapter.</summary>
    public static CannedSuggestionProvider IgnoringCancellation() =>
        new((_, _) => new TaskCompletionSource<SuggestionResult>().Task);

    public Task<SuggestionResult> GenerateAsync(SuggestionRequest request, CancellationToken cancellationToken)
    {
        CallCount++;
        return _respond(request, cancellationToken);
    }

    private static ExerciseContent BuildValidDraft(TrainingParameters parameters)
    {
        OptionKind? kind = parameters.Spread switch
        {
            ExperienceSpread.Anpassungen => OptionKind.Anpassung,
            ExperienceSpread.Varianten => OptionKind.Variante,
            _ => null
        };
        ExerciseOption[] options = kind is { } required
            ? [new ExerciseOption(required, "Vereinfachte Form", "Der Ball wird zugeworfen statt aufgeschlagen.")]
            : [];

        return new ExerciseContent(
            "Annahme in Dreiergruppen",
            "Zwei Personen nehmen an, eine spielt zu. Nach zehn Bällen wird rotiert.",
            Math.Min(15, parameters.DurationMinutes),
            Math.Min(3, parameters.ParticipantCount),
            null,
            [parameters.Level],
            parameters.FocusAreaIds,
            options);
    }
}
