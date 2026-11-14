namespace CoachFlow.Application.Ai;

/// <summary>Muss Prüfen und Reservieren atomar ausführen, sonst kann paralleler Zugriff das Budget überschreiten.</summary>
public interface IAiBudgetLedger
{
    /// <returns>Eine Reservierungs-ID, oder null, wenn der Betrag nicht mehr ins Budget passt.</returns>
    Task<Guid?> TryReserveAsync(string month, decimal amount, decimal budget, CancellationToken cancellationToken);

    /// <summary>Ersetzt den reservierten Betrag durch die tatsächlichen Kosten.</summary>
    Task SettleAsync(Guid reservationId, decimal actualCost, CancellationToken cancellationToken);
}

/// <summary>
/// Reserviert vor dem Aufruf die Maximalkosten. Bleiben die tatsächlichen Kosten unbekannt,
/// etwa nach einem Abbruch, bleibt die Reservierung bestehen. So zählt der Zweifel gegen das Budget.
/// </summary>
public sealed class AiBudgetGuard(IAiBudgetLedger ledger, AiOptions options, TimeProvider timeProvider)
{
    public bool IsEnabled => options.MonthlyBudget > 0;

    public Task<Guid?> TryReserveAsync(decimal maxCost, CancellationToken cancellationToken)
    {
        if (!IsEnabled) return Task.FromResult<Guid?>(null);
        var month = timeProvider.GetUtcNow().ToString("yyyy-MM");
        return ledger.TryReserveAsync(month, maxCost, options.MonthlyBudget, cancellationToken);
    }

    public Task SettleAsync(Guid reservationId, decimal? actualCost) =>
        actualCost is { } cost
            ? ledger.SettleAsync(reservationId, cost, CancellationToken.None)
            : Task.CompletedTask;
}
