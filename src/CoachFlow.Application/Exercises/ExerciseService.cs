using CoachFlow.Domain;

namespace CoachFlow.Application.Exercises;

public sealed class ExerciseService(
    IExerciseRepository exercises,
    IFocusAreaRepository focusAreas,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
{
    /// <summary>Feste Schwerpunkte laut Vorgabe; K0 Angabe, K1 Annahme, K2 Abwehr (Entscheidung K5).</summary>
    public static readonly IReadOnlyList<FocusArea> SystemFocusAreas =
    [
        new(Guid.Parse("00000000-0000-0000-0000-0000000000a0"), "K0", true),
        new(Guid.Parse("00000000-0000-0000-0000-0000000000a1"), "K1", true),
        new(Guid.Parse("00000000-0000-0000-0000-0000000000a2"), "K2", true),
        new(Guid.Parse("00000000-0000-0000-0000-0000000000a3"), "Abwehr", true),
        new(Guid.Parse("00000000-0000-0000-0000-0000000000a4"), "Mannschaftstaktik", true),
        new(Guid.Parse("00000000-0000-0000-0000-0000000000a5"), "Einzeltaktik", true)
    ];

    public Task<IReadOnlyList<Exercise>> ListAsync(CancellationToken cancellationToken) =>
        exercises.ListActiveAsync(cancellationToken);

    public async Task<ChangeOutcome<Exercise>> CreateAsync(Guid userId, ExerciseContent content, CancellationToken cancellationToken)
    {
        var errors = await GetErrorsAsync(content, cancellationToken);
        if (errors.Count > 0) return new ChangeOutcome<Exercise>.Invalid(errors);

        var exercise = new Exercise(Guid.NewGuid(), content, ExerciseOrigin.Manual, userId, timeProvider.GetUtcNow());
        exercises.Add(exercise);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return new ChangeOutcome<Exercise>.Done(exercise);
    }

    public async Task<ChangeOutcome<Exercise>> UpdateAsync(
        Guid exerciseId,
        int expectedVersion,
        ExerciseContent content,
        CancellationToken cancellationToken)
    {
        var errors = await GetErrorsAsync(content, cancellationToken);
        if (errors.Count > 0) return new ChangeOutcome<Exercise>.Invalid(errors);

        return await ChangeAsync(exerciseId, expectedVersion, e => e.Update(content, timeProvider.GetUtcNow()), cancellationToken);
    }

    public Task<ChangeOutcome<Exercise>> ArchiveAsync(Guid exerciseId, int expectedVersion, CancellationToken cancellationToken) =>
        ChangeAsync(exerciseId, expectedVersion, e => e.Archive(timeProvider.GetUtcNow()), cancellationToken);

    public async Task<IReadOnlyList<FocusArea>> ListFocusAreasAsync(CancellationToken cancellationToken)
    {
        var custom = await focusAreas.ListAsync(cancellationToken);
        return [.. SystemFocusAreas, .. custom.OrderBy(f => f.Name, StringComparer.CurrentCultureIgnoreCase)];
    }

    /// <summary>Legt einen eigenen Schwerpunkt an oder liefert den vorhandenen gleichen Namens.</summary>
    public async Task<ChangeOutcome<FocusArea>> AddFocusAreaAsync(string name, CancellationToken cancellationToken)
    {
        var trimmed = name.Trim();
        if (trimmed.Length is 0 or > 60)
            return new ChangeOutcome<FocusArea>.Invalid(["Der Name muss zwischen 1 und 60 Zeichen lang sein."]);

        var existing = (await ListFocusAreasAsync(cancellationToken))
            .FirstOrDefault(f => string.Equals(f.Name, trimmed, StringComparison.CurrentCultureIgnoreCase));
        if (existing is not null) return new ChangeOutcome<FocusArea>.Done(existing);

        var focusArea = new FocusArea(Guid.NewGuid(), trimmed, isSystem: false);
        focusAreas.Add(focusArea);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return new ChangeOutcome<FocusArea>.Done(focusArea);
    }

    private async Task<ChangeOutcome<Exercise>> ChangeAsync(
        Guid exerciseId,
        int expectedVersion,
        Action<Exercise> change,
        CancellationToken cancellationToken)
    {
        var exercise = await exercises.FindAsync(exerciseId, cancellationToken);
        if (exercise is null || exercise.IsArchived) return new ChangeOutcome<Exercise>.NotFound();
        if (exercise.Version != expectedVersion) return new ChangeOutcome<Exercise>.Conflict();

        exercises.ExpectVersion(exercise, expectedVersion);
        change(exercise);
        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (ConcurrencyConflictException)
        {
            return new ChangeOutcome<Exercise>.Conflict();
        }
        return new ChangeOutcome<Exercise>.Done(exercise);
    }

    private async Task<IReadOnlyList<string>> GetErrorsAsync(ExerciseContent content, CancellationToken cancellationToken)
    {
        var errors = ExerciseRules.CheckStructure(content).Select(f => f.Message).ToList();

        var known = (await ListFocusAreasAsync(cancellationToken)).Select(f => f.Id).ToHashSet();
        if (content.FocusAreaIds.Any(id => !known.Contains(id)))
            errors.Add("Ein gewählter Trainingsschwerpunkt existiert nicht.");
        return errors;
    }
}
