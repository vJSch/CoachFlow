using CoachFlow.Application.Ai;
using Microsoft.EntityFrameworkCore;

namespace CoachFlow.Infrastructure.Budget;

/// <summary>Summe aller Reservierungen und Kosten eines Monats.</summary>
public sealed class BudgetMonth
{
    public required string Month { get; init; }

    /// <summary>Millionstel Euro: exakt für Kosten unter einem Cent, auf jeder Datenbank als Ganzzahl berechenbar.</summary>
    public long UsedMicros { get; set; }
}

public sealed class BudgetReservation
{
    public Guid Id { get; init; }
    public required string Month { get; init; }
    public long ReservedMicros { get; init; }

    /// <summary>Tatsächliche Kosten; null, solange sie unbekannt sind. Dann zählt die Reservierung.</summary>
    public long? ActualMicros { get; set; }

    public DateTimeOffset CreatedAt { get; init; }
}

public sealed record BudgetMonthSummary(string Month, decimal Used, int Requests, int RequestsWithUnknownCost);

/// <summary>
/// Die Budgetprüfung ist eine einzige bedingte UPDATE-Anweisung auf der Monatszeile.
/// Die Datenbank sperrt dabei die Zeile, sodass parallele Reservierungen nacheinander geprüft werden.
/// </summary>
public sealed class EfAiBudgetLedger(CoachFlowDbContext db, TimeProvider timeProvider) : IAiBudgetLedger
{
    private const decimal MicrosPerEuro = 1_000_000m;

    public static long ToMicros(decimal euros) => (long)decimal.Ceiling(euros * MicrosPerEuro);
    public static decimal FromMicros(long micros) => micros / MicrosPerEuro;

    public async Task<Guid?> TryReserveAsync(string month, decimal amount, decimal budget, CancellationToken cancellationToken)
    {
        var amountMicros = ToMicros(amount);
        var budgetMicros = ToMicros(budget);
        await EnsureMonthAsync(month, cancellationToken);

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var updated = await db.BudgetMonths
            .Where(m => m.Month == month && m.UsedMicros + amountMicros <= budgetMicros)
            .ExecuteUpdateAsync(s => s.SetProperty(m => m.UsedMicros, m => m.UsedMicros + amountMicros), cancellationToken);
        if (updated == 0) return null;

        var reservation = new BudgetReservation
        {
            Id = Guid.NewGuid(),
            Month = month,
            ReservedMicros = amountMicros,
            CreatedAt = timeProvider.GetUtcNow()
        };
        db.BudgetReservations.Add(reservation);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        db.Entry(reservation).State = EntityState.Detached;
        return reservation.Id;
    }

    public async Task SettleAsync(Guid reservationId, decimal actualCost, CancellationToken cancellationToken)
    {
        var actualMicros = ToMicros(actualCost);

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var reservation = await db.BudgetReservations.SingleAsync(r => r.Id == reservationId, cancellationToken);
        if (reservation.ActualMicros is not null) return;

        // Liegen die Kosten über der Reservierung, wird trotzdem der echte Betrag gebucht:
        // Der Bericht soll die Wahrheit zeigen, und Folgeanfragen sperrt das Budget dann von selbst.
        var delta = actualMicros - reservation.ReservedMicros;
        await db.BudgetMonths
            .Where(m => m.Month == reservation.Month)
            .ExecuteUpdateAsync(s => s.SetProperty(m => m.UsedMicros, m => m.UsedMicros + delta), cancellationToken);

        reservation.ActualMicros = actualMicros;
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        db.Entry(reservation).State = EntityState.Detached;
    }

    /// <summary>Monatsbericht für die Betreiberin, ohne Anzeige in der Anwendung (Entscheidung K16).</summary>
    public async Task<BudgetMonthSummary> GetSummaryAsync(string month, CancellationToken cancellationToken)
    {
        var used = await db.BudgetMonths.Where(m => m.Month == month).Select(m => (long?)m.UsedMicros)
            .SingleOrDefaultAsync(cancellationToken) ?? 0;
        var requests = await db.BudgetReservations.CountAsync(r => r.Month == month, cancellationToken);
        var unknown = await db.BudgetReservations.CountAsync(r => r.Month == month && r.ActualMicros == null, cancellationToken);
        return new BudgetMonthSummary(month, FromMicros(used), requests, unknown);
    }

    private async Task EnsureMonthAsync(string month, CancellationToken cancellationToken)
    {
        if (await db.BudgetMonths.AnyAsync(m => m.Month == month, cancellationToken)) return;

        var row = new BudgetMonth { Month = month };
        db.BudgetMonths.Add(row);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Eine parallele Anfrage hat die Monatszeile gerade angelegt; das ist erwünscht.
        }
        finally
        {
            db.Entry(row).State = EntityState.Detached;
        }
    }
}
