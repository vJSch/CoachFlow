namespace CoachFlow.Domain;

public sealed record StoredPlanInfo(Guid Id, string Title, DateTimeOffset CreatedAt);

public abstract record PlanLimitDecision
{
    public sealed record Allowed : PlanLimitDecision;

    /// <summary>Der älteste Plan wird gelöscht, aber nur nach ausdrücklicher Bestätigung.</summary>
    public sealed record ConfirmationRequired(StoredPlanInfo PlanToDelete) : PlanLimitDecision;
}

/// <summary>
/// Entscheidung K14: Höchstens zehn Pläne je Nutzer, Entwürfe zählen mit.
/// Beim elften wird nach einer Warnung der älteste Plan gelöscht.
/// </summary>
public static class PlanLimitPolicy
{
    public const int MaxPlansPerUser = 10;

    public static PlanLimitDecision Evaluate(IReadOnlyCollection<StoredPlanInfo> existingPlans)
    {
        if (existingPlans.Count < MaxPlansPerUser) return new PlanLimitDecision.Allowed();

        var oldest = existingPlans.OrderBy(p => p.CreatedAt).ThenBy(p => p.Id).First();
        return new PlanLimitDecision.ConfirmationRequired(oldest);
    }
}
