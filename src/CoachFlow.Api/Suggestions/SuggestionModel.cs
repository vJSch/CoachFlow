using CoachFlow.Api.Accounts;
using CoachFlow.Api.Exercises;
using CoachFlow.Api.Plans;

namespace CoachFlow.Api.Suggestions;

public class AiOptions
{
    public const string Section = "Ai";

    // "None": abgeschaltet. "Test": vorbereitete Antworten ohne externen Dienst.
    public string Provider { get; set; } = SuggestionProviders.None;

    // Leer heißt: kein Budget gesetzt. Für einen echten Anbieter wird es Pflicht.
    public decimal? MonthlyBudgetEuro { get; set; }

    // Etwas kürzer als die 15 Sekunden der Oberfläche, damit noch eine geordnete Antwort ankommt.
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(13);

    public int RequestsPerMinute { get; set; } = 5;

    public int SuggestionCount { get; set; } = 3;

    public TestProviderOptions Test { get; set; } = new();
}

public class TestProviderOptions
{
    // Zum Ausprobieren der Wartemeldungen, z. B. 7 Sekunden für den Hinweis oder 20 für das Zeitlimit.
    public TimeSpan Delay { get; set; } = TimeSpan.FromSeconds(1);
}

public static class SuggestionProviders
{
    public const string None = "None";
    public const string Test = "Test";
}

// Alles, was den Server in Richtung KI verlässt. Bewusst ohne Konto-, Plan- oder Notizdaten.
public sealed record ProviderInput(
    int ParticipantCount,
    Level Level,
    Spread Spread,
    int DurationMinutes,
    IReadOnlyList<string> FocusNames,
    int SuggestionCount);

// Rohantwort des Anbieters. Das Format prüft der SuggestionParser, nicht der Adapter.
public sealed record ProviderResult(string Json, int InputTokens, int OutputTokens, decimal EstimatedCostEuro);

public enum ProviderFailure
{
    Unavailable,
    Overloaded
}

public sealed class SuggestionProviderException(ProviderFailure failure, string message, Exception? inner = null)
    : Exception(message, inner)
{
    public ProviderFailure Failure { get; } = failure;
}

public interface IExerciseSuggestionProvider
{
    Task<ProviderResult> SuggestAsync(ProviderInput input, CancellationToken ct);
}

public sealed record ExerciseSuggestion(
    string Name,
    string Description,
    int DefaultDurationMinutes,
    int? MinParticipants,
    int? MaxParticipants);

public enum AiUsageResult
{
    Success,
    InvalidResponse,
    ProviderError,
    TimedOut,
    Cancelled
}

// Grundlage für das Monatsbudget und für spätere Kostenauswertungen.
public class AiUsage
{
    public Guid Id { get; set; }

    public string? UserId { get; set; }

    public AppUser? User { get; set; }

    public DateTime CreatedAt { get; set; }

    public string Provider { get; set; } = "";

    public AiUsageResult Result { get; set; }

    public int InputTokens { get; set; }

    public int OutputTokens { get; set; }

    public decimal EstimatedCostEuro { get; set; }
}
