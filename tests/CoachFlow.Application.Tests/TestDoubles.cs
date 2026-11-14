using CoachFlow.Application.Ai;
using CoachFlow.Application.Exercises;
using CoachFlow.Application.Suggestions;

namespace CoachFlow.Application.Tests;

/// <summary>Testdouble. Die Datenbankversion wird in CoachFlow.Infrastructure.Tests gegen dieselben Zusagen geprüft.</summary>
public sealed class InMemoryBudgetLedger : IAiBudgetLedger
{
    private readonly Lock _lock = new();
    private readonly Dictionary<Guid, (string Month, decimal Amount)> _entries = [];

    public decimal Total(string month)
    {
        lock (_lock) return _entries.Values.Where(e => e.Month == month).Sum(e => e.Amount);
    }

    public Task<Guid?> TryReserveAsync(string month, decimal amount, decimal budget, CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            var used = _entries.Values.Where(e => e.Month == month).Sum(e => e.Amount);
            if (used + amount > budget) return Task.FromResult<Guid?>(null);

            var id = Guid.NewGuid();
            _entries[id] = (month, amount);
            return Task.FromResult<Guid?>(id);
        }
    }

    public Task SettleAsync(Guid reservationId, decimal actualCost, CancellationToken cancellationToken)
    {
        lock (_lock) _entries[reservationId] = (_entries[reservationId].Month, actualCost);
        return Task.CompletedTask;
    }
}

public sealed class InMemorySuggestions : ISuggestionRepository
{
    private readonly Dictionary<Guid, AiSuggestion> _items = [];

    public Task<AiSuggestion?> FindAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(_items.GetValueOrDefault(id));

    public void Add(AiSuggestion suggestion) => _items[suggestion.Id] = suggestion;
}

public sealed class InMemoryExercises : IExerciseRepository
{
    public List<Exercise> Items { get; } = [];

    public Task<Exercise?> FindAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Items.SingleOrDefault(e => e.Id == id));

    public Task<IReadOnlyList<Exercise>> ListActiveAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Exercise>>(Items.Where(e => !e.IsArchived).ToList());

    public void Add(Exercise exercise) => Items.Add(exercise);

    public void ExpectVersion(Exercise exercise, int expectedVersion) { }
}

public sealed class NoOpUnitOfWork : IUnitOfWork
{
    public int SaveCount { get; private set; }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        SaveCount++;
        return Task.CompletedTask;
    }
}
