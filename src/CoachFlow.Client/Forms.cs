using CoachFlow.Domain;

namespace CoachFlow.Client;

// Veränderliche Formularmodelle für die Oberfläche. Die Prüfregeln kommen unverändert aus der Domäne,
// sodass die Oberfläche dieselben Befunde zeigt, die der Server prüft.

public sealed class OptionModel
{
    public OptionKind Kind { get; set; } = OptionKind.Anpassung;
    public string? Title { get; set; }
    public string? Description { get; set; }
    public Guid? ReferencedExerciseId { get; set; }

    public ExerciseOption ToOption() => new(Kind, Clean(Title), Clean(Description), ReferencedExerciseId);

    public static OptionModel From(ExerciseOption option) => new()
    {
        Kind = option.Kind,
        Title = option.Title,
        Description = option.Description,
        ReferencedExerciseId = option.ReferencedExerciseId
    };

    internal static string? Clean(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();
}

public sealed class ExerciseModel
{
    public string? Title { get; set; }
    public string? Description { get; set; }
    public int? DurationMinutes { get; set; }
    public int? MinParticipants { get; set; }
    public int? MaxParticipants { get; set; }
    public List<ExperienceLevel> Levels { get; } = [];
    public List<Guid> FocusAreaIds { get; } = [];
    public List<OptionModel> Options { get; } = [];

    public ExerciseContent ToContent() => new(
        OptionModel.Clean(Title),
        OptionModel.Clean(Description),
        DurationMinutes,
        MinParticipants,
        MaxParticipants,
        Levels.ToList(),
        FocusAreaIds.ToList(),
        Options.Select(o => o.ToOption()).ToList());

    public IReadOnlyList<Finding> StructureFindings => ExerciseRules.CheckStructure(ToContent());

    public static ExerciseModel From(ExerciseContent content)
    {
        var model = new ExerciseModel
        {
            Title = content.Title,
            Description = content.Description,
            DurationMinutes = content.DurationMinutes,
            MinParticipants = content.MinParticipants,
            MaxParticipants = content.MaxParticipants
        };
        model.Levels.AddRange(content.Levels);
        model.FocusAreaIds.AddRange(content.FocusAreaIds);
        model.Options.AddRange(content.Options.Select(OptionModel.From));
        return model;
    }

    public void ToggleLevel(ExperienceLevel level)
    {
        if (!Levels.Remove(level)) Levels.Add(level);
    }

    /// <returns><c>false</c>, wenn ein dritter Schwerpunkt gewählt würde.</returns>
    public bool ToggleFocusArea(Guid id) => FocusSelection.Toggle(FocusAreaIds, id);
}

public sealed class ParametersModel
{
    public int ParticipantCount { get; set; } = 10;
    public ExperienceLevel Level { get; set; } = ExperienceLevel.Hobby;
    public ExperienceSpread Spread { get; set; } = ExperienceSpread.Keine;
    public int DurationMinutes { get; set; } = 90;
    public List<Guid> FocusAreaIds { get; } = [];

    public TrainingParameters ToParameters() => new(ParticipantCount, Level, Spread, DurationMinutes, FocusAreaIds.ToList());

    public bool ToggleFocusArea(Guid id) => FocusSelection.Toggle(FocusAreaIds, id);

    public static ParametersModel From(TrainingParameters parameters)
    {
        var model = new ParametersModel
        {
            ParticipantCount = parameters.ParticipantCount,
            Level = parameters.Level,
            Spread = parameters.Spread,
            DurationMinutes = parameters.DurationMinutes
        };
        model.FocusAreaIds.AddRange(parameters.FocusAreaIds);
        return model;
    }
}

public sealed class PlanItemModel
{
    /// <summary>Stabiler Schlüssel für das Rendern beim Umsortieren.</summary>
    public Guid Key { get; } = Guid.NewGuid();
    public Guid? SourceExerciseId { get; init; }
    public required ExerciseContent Exercise { get; init; }
    public int PlannedMinutes { get; set; }
    public string? Note { get; set; }

    public PlanItemDto ToDto() => new(SourceExerciseId, Exercise, PlannedMinutes, OptionModel.Clean(Note));
}

public sealed class PlanModel
{
    public const int DefaultMinutes = 10;

    public Guid? Id { get; private set; }
    public int Version { get; set; }
    public string Title { get; set; } = "";
    public ParametersModel Parameters { get; private set; } = new();
    public List<PlanItemModel> Items { get; } = [];

    public bool IsNew => Id is null;

    public PlanInput ToInput() => new(Title.Trim(), Parameters.ToParameters(), Items.Select(i => i.ToDto()).ToList());

    private IReadOnlyList<PlanEntry> Entries => Items.Select(i => i.ToDto().ToEntry()).ToList();

    public int TotalMinutes => PlanRules.TotalMinutes(Entries);
    public int RemainingMinutes => PlanRules.RemainingMinutes(Parameters.ToParameters(), Entries);
    public IReadOnlyList<Finding> Findings => PlanRules.Check(Parameters.ToParameters(), Entries);

    /// <summary>Übernimmt eine Kopie des Inhalts; spätere Änderungen im Bestand ändern den Plan nicht.</summary>
    public void Add(Guid? sourceExerciseId, ExerciseContent content) => Items.Add(new PlanItemModel
    {
        SourceExerciseId = sourceExerciseId,
        Exercise = content,
        PlannedMinutes = content.DurationMinutes ?? DefaultMinutes
    });

    public void Move(int index, int delta)
    {
        var target = index + delta;
        if (index < 0 || index >= Items.Count || target < 0 || target >= Items.Count) return;
        (Items[index], Items[target]) = (Items[target], Items[index]);
    }

    public void RemoveAt(int index)
    {
        if (index >= 0 && index < Items.Count) Items.RemoveAt(index);
    }

    public void Saved(PlanDto plan)
    {
        Id = plan.Id;
        Version = plan.Version;
    }

    public static PlanModel From(PlanDto plan)
    {
        var model = new PlanModel { Title = plan.Title, Parameters = ParametersModel.From(plan.Parameters) };
        model.Saved(plan);
        foreach (var item in plan.Items)
            model.Items.Add(new PlanItemModel
            {
                SourceExerciseId = item.SourceExerciseId,
                Exercise = item.Exercise,
                PlannedMinutes = item.PlannedMinutes,
                Note = item.Note
            });
        return model;
    }
}

internal static class FocusSelection
{
    public static bool Toggle(List<Guid> selected, Guid id)
    {
        if (selected.Remove(id)) return true;
        if (selected.Count >= TrainingParameters.MaxFocusAreas) return false;
        selected.Add(id);
        return true;
    }
}
