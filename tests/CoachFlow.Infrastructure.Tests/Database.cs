using CoachFlow.AiAdapters.Canned;
using CoachFlow.Application.Ai;
using CoachFlow.Application.Exercises;
using CoachFlow.Application.Plans;
using CoachFlow.Application.Suggestions;
using CoachFlow.Infrastructure.Budget;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;

namespace CoachFlow.Infrastructure.Tests;

/// <summary>
/// Eigene SQLite-Datei je Test. Jeder Aufruf von <see cref="Context"/> öffnet einen neuen Kontext
/// mit eigener Verbindung, so wie zwei getrennte HTTP-Anfragen. PostgreSQL-Tests folgen, sobald Docker läuft.
/// </summary>
public sealed class Database : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"coachflow-test-{Guid.NewGuid():N}.db");

    public Database()
    {
        using var db = Context();
        db.Database.EnsureCreated();
    }

    public FakeTimeProvider Time { get; } = new(new DateTimeOffset(2026, 11, 8, 10, 0, 0, TimeSpan.Zero));

    public CoachFlowDbContext Context() =>
        new(new DbContextOptionsBuilder<CoachFlowDbContext>()
            .UseSqlite($"Data Source={_path};Pooling=False;Default Timeout=30")
            .Options);

    public PlanService Plans(CoachFlowDbContext db) =>
        new(new EfPlanRepository(db), new EfUnitOfWork(db), Time);

    public ExerciseService Exercises(CoachFlowDbContext db) =>
        new(new EfExerciseRepository(db), new EfFocusAreaRepository(db), new EfUnitOfWork(db), Time);

    public SuggestionService Suggestions(CoachFlowDbContext db, IExerciseSuggestionProvider provider, AiRequestGate? gate = null)
    {
        var options = new AiOptions { MonthlyBudget = 50m };
        return new SuggestionService(
            provider,
            new AiBudgetGuard(new EfAiBudgetLedger(db, Time), options, Time),
            gate ?? new AiRequestGate(options, Time),
            new EfSuggestionRepository(db),
            new EfExerciseRepository(db),
            new EfUnitOfWork(db),
            options,
            Time);
    }

    public static CannedSuggestionProvider ValidProvider() => CannedSuggestionProvider.WithValidAnswer();

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        try { File.Delete(_path); } catch (IOException) { }
    }
}
