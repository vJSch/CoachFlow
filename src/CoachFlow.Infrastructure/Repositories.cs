using CoachFlow.Application;
using CoachFlow.Application.Exercises;
using CoachFlow.Application.Plans;
using CoachFlow.Application.Suggestions;
using Microsoft.EntityFrameworkCore;

namespace CoachFlow.Infrastructure;

public sealed class EfUnitOfWork(CoachFlowDbContext db) : IUnitOfWork
{
    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            // Der Kontext wird pro Anfrage neu erzeugt, die verworfenen Änderungen bleiben also nicht hängen.
            foreach (var entry in ex.Entries) entry.State = EntityState.Detached;
            throw new ConcurrencyConflictException(ex);
        }
    }
}

public sealed class EfExerciseRepository(CoachFlowDbContext db) : IExerciseRepository
{
    public Task<Exercise?> FindAsync(Guid id, CancellationToken cancellationToken) =>
        db.Exercises.SingleOrDefaultAsync(e => e.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Exercise>> ListActiveAsync(CancellationToken cancellationToken) =>
        await db.Exercises.Where(e => e.ArchivedAt == null).ToListAsync(cancellationToken);

    public void Add(Exercise exercise) => db.Exercises.Add(exercise);

    public void ExpectVersion(Exercise exercise, int expectedVersion) =>
        db.Entry(exercise).Property(e => e.Version).OriginalValue = expectedVersion;
}

public sealed class EfFocusAreaRepository(CoachFlowDbContext db) : IFocusAreaRepository
{
    public async Task<IReadOnlyList<FocusArea>> ListAsync(CancellationToken cancellationToken) =>
        await db.FocusAreas.ToListAsync(cancellationToken);

    public void Add(FocusArea focusArea) => db.FocusAreas.Add(focusArea);
}

public sealed class EfPlanRepository(CoachFlowDbContext db) : IPlanRepository
{
    public Task<TrainingPlan?> FindAsync(Guid id, CancellationToken cancellationToken) =>
        db.Plans.SingleOrDefaultAsync(p => p.Id == id, cancellationToken);

    public async Task<IReadOnlyList<TrainingPlan>> ListByOwnerAsync(Guid ownerId, CancellationToken cancellationToken) =>
        await db.Plans.Where(p => p.OwnerId == ownerId).ToListAsync(cancellationToken);

    public void Add(TrainingPlan plan) => db.Plans.Add(plan);

    public void Remove(TrainingPlan plan) => db.Plans.Remove(plan);

    public void ExpectVersion(TrainingPlan plan, int expectedVersion) =>
        db.Entry(plan).Property(p => p.Version).OriginalValue = expectedVersion;
}

public sealed class EfSuggestionRepository(CoachFlowDbContext db) : ISuggestionRepository
{
    public Task<AiSuggestion?> FindAsync(Guid id, CancellationToken cancellationToken) =>
        db.Suggestions.SingleOrDefaultAsync(s => s.Id == id, cancellationToken);

    public void Add(AiSuggestion suggestion) => db.Suggestions.Add(suggestion);
}
