using CoachFlow.Api.Accounts;
using CoachFlow.Api.Exercises;

namespace CoachFlow.Api.Plans;

public enum Spread
{
    Low,
    Medium,
    High
}

public class TrainingPlan
{
    public const int MaxFocuses = 2;

    public Guid Id { get; set; }

    public string Sport { get; set; } = Sports.Volleyball;

    public string Title { get; set; } = "";

    public int ParticipantCount { get; set; }

    public Level Level { get; set; }

    // Wie unterschiedlich stark die Gruppe ist.
    public Spread Spread { get; set; }

    public int DurationMinutes { get; set; }

    public string Note { get; set; } = "";

    public List<Focus> Focuses { get; set; } = [];

    public List<PlanPosition> Positions { get; set; } = [];

    public int Version { get; set; }

    public string? UpdatedById { get; set; }

    public AppUser? UpdatedBy { get; set; }

    public DateTime UpdatedAt { get; set; }
}

public class PlanPosition
{
    public Guid Id { get; set; }

    public Guid PlanId { get; set; }

    public int SortOrder { get; set; }

    // Verweis auf die Ursprungsübung. Wird leer, wenn die Übung gelöscht wird.
    public Guid? ExerciseId { get; set; }

    // Kopie beim ersten Speichern. Spätere Änderungen der Übung ändern den Plan nicht.
    public string Name { get; set; } = "";

    public string Description { get; set; } = "";

    public int PlannedDurationMinutes { get; set; }

    public string Note { get; set; } = "";
}
