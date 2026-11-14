namespace CoachFlow.Application.Ai;

public sealed class AiOptions
{
    /// <summary>Monatsbudget. Der Standardwert 0 sperrt die KI, solange kein Betrag festgelegt ist.</summary>
    public decimal MonthlyBudget { get; init; }

    /// <summary>Liegt unter den 15 s des Clients, damit die Meldung des Servers rechtzeitig ankommt.</summary>
    public TimeSpan ServerDeadline { get; init; } = TimeSpan.FromSeconds(13);

    public int MaxRequestsPerMinutePerUser { get; init; } = 5;
}
