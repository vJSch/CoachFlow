namespace CoachFlow.Application;

/// <summary>Schreibt alle vorgemerkten Änderungen in einer Transaktion.</summary>
public interface IUnitOfWork
{
    /// <exception cref="ConcurrencyConflictException">Ein Datensatz wurde zwischenzeitlich geändert.</exception>
    Task SaveChangesAsync(CancellationToken cancellationToken);
}

public sealed class ConcurrencyConflictException(Exception? inner = null)
    : Exception("Der Datensatz wurde zwischenzeitlich geändert.", inner);

/// <summary>Gemeinsame Ergebnisse für Änderungen an gespeicherten Daten.</summary>
public abstract record ChangeOutcome<T>
{
    public sealed record Done(T Value) : ChangeOutcome<T>;
    public sealed record Invalid(IReadOnlyList<string> Errors) : ChangeOutcome<T>;
    public sealed record NotFound : ChangeOutcome<T>;

    /// <summary>Jemand anderes hat den Datensatz inzwischen geändert; die Eingaben werden nicht verworfen.</summary>
    public sealed record Conflict : ChangeOutcome<T>;
}

public static class ConflictMessages
{
    public const string Changed = "Dieser Eintrag wurde inzwischen geändert. Bitte lade ihn neu; deine Eingaben bleiben im Formular erhalten.";
}
