using CoachFlow.Domain;

namespace CoachFlow.Application.Plans;

public sealed record PlanInput(string Title, TrainingParameters Parameters, IReadOnlyList<PlanItem> Items);

public abstract record CreatePlanOutcome
{
    public sealed record Created(TrainingPlan Plan, StoredPlanInfo? DeletedPlan) : CreatePlanOutcome;
    public sealed record Invalid(IReadOnlyList<string> Errors) : CreatePlanOutcome;

    /// <summary>Warnung nach K14: Der genannte Plan würde gelöscht. Erneut senden mit dessen Id als Bestätigung.</summary>
    public sealed record ConfirmationRequired(StoredPlanInfo PlanToDelete, string Message) : CreatePlanOutcome;
}

public sealed class PlanService(IPlanRepository plans, IUnitOfWork unitOfWork, TimeProvider timeProvider)
{
    public const int MaxTitleLength = 120;

    public async Task<IReadOnlyList<TrainingPlan>> ListAsync(Guid userId, CancellationToken cancellationToken) =>
        (await plans.ListByOwnerAsync(userId, cancellationToken)).OrderByDescending(p => p.UpdatedAt).ToList();

    public async Task<TrainingPlan?> GetAsync(Guid userId, Guid planId, CancellationToken cancellationToken)
    {
        var plan = await plans.FindAsync(planId, cancellationToken);
        return plan?.OwnerId == userId ? plan : null;
    }

    /// <param name="confirmedDeletionId">
    /// Id des Plans, dessen Löschung nach der Warnung bestätigt wurde. Sie muss genau dem
    /// aktuell ältesten Plan entsprechen, damit nie ein anderer Plan gelöscht wird als angekündigt.
    /// </param>
    public async Task<CreatePlanOutcome> CreateAsync(
        Guid userId,
        PlanInput input,
        Guid? confirmedDeletionId,
        CancellationToken cancellationToken)
    {
        var errors = GetErrors(input);
        if (errors.Count > 0) return new CreatePlanOutcome.Invalid(errors);

        var existing = await plans.ListByOwnerAsync(userId, cancellationToken);
        StoredPlanInfo? deleted = null;

        if (PlanLimitPolicy.Evaluate(existing.Select(p => p.ToInfo()).ToList())
            is PlanLimitDecision.ConfirmationRequired { PlanToDelete: var oldest })
        {
            if (confirmedDeletionId != oldest.Id)
                return new CreatePlanOutcome.ConfirmationRequired(oldest,
                    $"Du hast bereits {PlanLimitPolicy.MaxPlansPerUser} Pläne. Beim Speichern wird dein ältester Plan „{oldest.Title}“ gelöscht.");

            plans.Remove(existing.Single(p => p.Id == oldest.Id));
            deleted = oldest;
        }

        var plan = new TrainingPlan(Guid.NewGuid(), userId, input.Title.Trim(), input.Parameters, input.Items, timeProvider.GetUtcNow());
        plans.Add(plan);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return new CreatePlanOutcome.Created(plan, deleted);
    }

    public async Task<ChangeOutcome<TrainingPlan>> UpdateAsync(
        Guid userId,
        Guid planId,
        int expectedVersion,
        PlanInput input,
        CancellationToken cancellationToken)
    {
        var errors = GetErrors(input);
        if (errors.Count > 0) return new ChangeOutcome<TrainingPlan>.Invalid(errors);

        var plan = await GetAsync(userId, planId, cancellationToken);
        if (plan is null) return new ChangeOutcome<TrainingPlan>.NotFound();
        if (plan.Version != expectedVersion) return new ChangeOutcome<TrainingPlan>.Conflict();

        plans.ExpectVersion(plan, expectedVersion);
        plan.Update(input.Title.Trim(), input.Parameters, input.Items, timeProvider.GetUtcNow());
        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (ConcurrencyConflictException)
        {
            return new ChangeOutcome<TrainingPlan>.Conflict();
        }
        return new ChangeOutcome<TrainingPlan>.Done(plan);
    }

    public async Task<bool> DeleteAsync(Guid userId, Guid planId, CancellationToken cancellationToken)
    {
        var plan = await GetAsync(userId, planId, cancellationToken);
        if (plan is null) return false;

        plans.Remove(plan);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    private static List<string> GetErrors(PlanInput input)
    {
        var errors = input.Parameters.GetErrors(forAiRequest: false).ToList();
        if (string.IsNullOrWhiteSpace(input.Title)) errors.Add("Der Plan braucht einen Titel.");
        else if (input.Title.Trim().Length > MaxTitleLength) errors.Add($"Der Titel darf höchstens {MaxTitleLength} Zeichen lang sein.");
        if (input.Items.Any(i => i.PlannedMinutes < 1)) errors.Add("Jede Übung im Plan braucht mindestens eine Minute.");
        return errors;
    }
}
