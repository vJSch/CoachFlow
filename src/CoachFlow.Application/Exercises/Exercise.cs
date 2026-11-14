using CoachFlow.Domain;

namespace CoachFlow.Application.Exercises;

public enum ExerciseOrigin
{
    Manual,
    Ai
}

/// <summary>
/// Übung im gemeinsamen Bestand (Entscheidung K13: alle Angemeldeten dürfen bearbeiten).
/// <see cref="Version"/> verhindert, dass parallele Änderungen sich stillschweigend überschreiben.
/// </summary>
public sealed class Exercise
{
    public Exercise(Guid id, ExerciseContent content, ExerciseOrigin origin, Guid createdBy, DateTimeOffset createdAt)
    {
        Id = id;
        Content = content;
        Origin = origin;
        CreatedBy = createdBy;
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
    }

    public Guid Id { get; private set; }
    public ExerciseContent Content { get; private set; }
    public ExerciseOrigin Origin { get; private set; }
    public Guid CreatedBy { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>Archivierte Übungen erscheinen nicht mehr in der Auswahl; Pläne enthalten ohnehin eine Kopie.</summary>
    public DateTimeOffset? ArchivedAt { get; private set; }

    public int Version { get; private set; } = 1;

    public bool IsArchived => ArchivedAt is not null;

    public void Update(ExerciseContent content, DateTimeOffset now)
    {
        Content = content;
        Touch(now);
    }

    public void Archive(DateTimeOffset now)
    {
        ArchivedAt = now;
        Touch(now);
    }

    private void Touch(DateTimeOffset now)
    {
        UpdatedAt = now;
        Version++;
    }
}

public sealed class FocusArea(Guid id, string name, bool isSystem)
{
    public Guid Id { get; private set; } = id;
    public string Name { get; private set; } = name;
    public bool IsSystem { get; private set; } = isSystem;
}

public interface IExerciseRepository
{
    Task<Exercise?> FindAsync(Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyList<Exercise>> ListActiveAsync(CancellationToken cancellationToken);
    void Add(Exercise exercise);

    /// <summary>Wird gesetzt, wenn die Version vom geladenen Stand abweicht, damit der Konflikt beim Speichern erkannt wird.</summary>
    void ExpectVersion(Exercise exercise, int expectedVersion);
}

public interface IFocusAreaRepository
{
    Task<IReadOnlyList<FocusArea>> ListAsync(CancellationToken cancellationToken);
    void Add(FocusArea focusArea);
}
