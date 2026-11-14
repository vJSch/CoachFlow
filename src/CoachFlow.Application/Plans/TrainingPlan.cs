using CoachFlow.Domain;

namespace CoachFlow.Application.Plans;

/// <summary>
/// Eintrag eines Plans mit einer Kopie der Übung. So verändert eine spätere Bearbeitung
/// im gemeinsamen Bestand keinen gespeicherten Plan.
/// </summary>
public sealed record PlanItem(Guid? SourceExerciseId, ExerciseContent Exercise, int PlannedMinutes, string? Note)
{
    public PlanEntry ToEntry() => new(
        Exercise.Title ?? "Ohne Titel",
        PlannedMinutes,
        Exercise.MinParticipants,
        Exercise.MaxParticipants,
        Exercise.Options);
}

public sealed class TrainingPlan
{
    public TrainingPlan(
        Guid id,
        Guid ownerId,
        string title,
        TrainingParameters parameters,
        IReadOnlyList<PlanItem> items,
        DateTimeOffset createdAt)
    {
        Id = id;
        OwnerId = ownerId;
        Title = title;
        Parameters = parameters;
        Items = items;
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
    }

    public Guid Id { get; private set; }
    public Guid OwnerId { get; private set; }
    public string Title { get; private set; }
    public TrainingParameters Parameters { get; private set; }
    public IReadOnlyList<PlanItem> Items { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public int Version { get; private set; } = 1;

    public int TotalMinutes => PlanRules.TotalMinutes(Items.Select(i => i.ToEntry()));

    /// <summary>Hinweise zum Plan. Sie verhindern das Speichern nie.</summary>
    public IReadOnlyList<Finding> Findings => PlanRules.Check(Parameters, Items.Select(i => i.ToEntry()).ToList());

    public StoredPlanInfo ToInfo() => new(Id, Title, CreatedAt);

    public void Update(string title, TrainingParameters parameters, IReadOnlyList<PlanItem> items, DateTimeOffset now)
    {
        Title = title;
        Parameters = parameters;
        Items = items;
        UpdatedAt = now;
        Version++;
    }
}

public interface IPlanRepository
{
    Task<TrainingPlan?> FindAsync(Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyList<TrainingPlan>> ListByOwnerAsync(Guid ownerId, CancellationToken cancellationToken);
    void Add(TrainingPlan plan);
    void Remove(TrainingPlan plan);
    void ExpectVersion(TrainingPlan plan, int expectedVersion);
}
