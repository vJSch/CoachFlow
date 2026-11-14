using CoachFlow.Application.Ai;
using CoachFlow.Application.Exercises;
using CoachFlow.Domain;

namespace CoachFlow.Application.Suggestions;

public enum RequestDenial
{
    InvalidInput,
    Disabled,
    AlreadyRunning,
    RateLimited,
    BudgetExhausted,
    TimedOut,
    Cancelled,
    ProviderFailed,
    InvalidResponse
}

public abstract record RequestOutcome
{
    public sealed record Created(AiSuggestion Suggestion) : RequestOutcome;
    public sealed record Denied(RequestDenial Reason, string Message) : RequestOutcome;
}

public abstract record AdoptOutcome
{
    public sealed record Adopted(Exercise Exercise) : AdoptOutcome;
    public sealed record Blocked(IReadOnlyList<Finding> Findings) : AdoptOutcome;
    public sealed record NotAvailable : AdoptOutcome;
}

public static class SuggestionMessages
{
    /// <summary>Wortlaut aus Entscheidung K12.</summary>
    public const string TimedOut = "Es konnte rechtzeitig kein Übungsvorschlag erstellt werden. Deine Eingaben bleiben erhalten.";
    public const string Disabled = "KI-Vorschläge sind derzeit nicht verfügbar. Du kannst mit vorhandenen Übungen weiterplanen.";
    public const string AlreadyRunning = "Es läuft bereits eine KI-Anfrage. Bitte warte, bis sie abgeschlossen ist.";
    public const string RateLimited = "Du hast das Limit von KI-Anfragen pro Minute erreicht. Bitte versuche es gleich noch einmal.";
    public const string BudgetExhausted = "Das KI-Budget für diesen Monat ist ausgeschöpft. Du kannst mit vorhandenen Übungen weiterplanen.";
    public const string Cancelled = "Die Anfrage wurde abgebrochen.";
    public const string ProviderFailed = "Die KI ist gerade nicht erreichbar. Du kannst mit vorhandenen Übungen weiterplanen.";
    public const string InvalidResponse = "Die KI hat keine verwertbare Antwort geliefert. Bitte versuche es erneut.";
}

public sealed class SuggestionService(
    IExerciseSuggestionProvider provider,
    AiBudgetGuard budget,
    AiRequestGate gate,
    ISuggestionRepository suggestions,
    IExerciseRepository exercises,
    IUnitOfWork unitOfWork,
    AiOptions options,
    TimeProvider timeProvider)
{
    public async Task<RequestOutcome> RequestAsync(
        Guid userId,
        SuggestionRequest request,
        CancellationToken cancellationToken)
    {
        var errors = request.Parameters.GetErrors(forAiRequest: true);
        if (errors.Count > 0) return Denied(RequestDenial.InvalidInput, string.Join(" ", errors));
        if (!budget.IsEnabled) return Denied(RequestDenial.Disabled, SuggestionMessages.Disabled);

        switch (gate.TryEnter(userId))
        {
            case GateDenial.AlreadyRunning: return Denied(RequestDenial.AlreadyRunning, SuggestionMessages.AlreadyRunning);
            case GateDenial.RateLimited: return Denied(RequestDenial.RateLimited, SuggestionMessages.RateLimited);
        }

        try
        {
            var reservation = await budget.TryReserveAsync(provider.MaxCostPerRequest, cancellationToken);
            if (reservation is not { } reservationId)
                return Denied(RequestDenial.BudgetExhausted, SuggestionMessages.BudgetExhausted);

            return await CallProviderAsync(userId, request, reservationId, cancellationToken);
        }
        finally
        {
            gate.Leave(userId);
        }
    }

    public async Task<IReadOnlyList<Finding>?> EditAsync(
        Guid userId,
        Guid suggestionId,
        ExerciseContent draft,
        CancellationToken cancellationToken)
    {
        var suggestion = await FindPendingAsync(userId, suggestionId, cancellationToken);
        if (suggestion is null) return null;

        suggestion.Edit(draft);
        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (ConcurrencyConflictException)
        {
            return null;
        }
        return suggestion.Findings;
    }

    public async Task<bool> DiscardAsync(Guid userId, Guid suggestionId, CancellationToken cancellationToken)
    {
        var suggestion = await FindPendingAsync(userId, suggestionId, cancellationToken);
        if (suggestion is null) return false;

        suggestion.Discard();
        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (ConcurrencyConflictException)
        {
            return false;
        }
        return true;
    }

    /// <summary>Prüft immer die aktuelle Fassung, auch wenn sie zuvor bereits angezeigt wurde.</summary>
    public async Task<AdoptOutcome> AdoptAsync(Guid userId, Guid suggestionId, CancellationToken cancellationToken)
    {
        var suggestion = await FindPendingAsync(userId, suggestionId, cancellationToken);
        if (suggestion is null) return new AdoptOutcome.NotAvailable();

        var findings = suggestion.Findings;
        if (SuggestionValidator.HasBlockers(findings)) return new AdoptOutcome.Blocked(findings);

        var exercise = new Exercise(
            Guid.NewGuid(),
            suggestion.Draft,
            ExerciseOrigin.Ai,
            userId,
            timeProvider.GetUtcNow());

        // Übung und Statuswechsel werden gemeinsam gespeichert. Klickt jemand doppelt,
        // scheitert der zweite Versuch an der Version, statt eine zweite Übung anzulegen.
        suggestion.MarkAdopted(exercise.Id);
        exercises.Add(exercise);
        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (ConcurrencyConflictException)
        {
            return new AdoptOutcome.NotAvailable();
        }
        return new AdoptOutcome.Adopted(exercise);
    }

    private async Task<RequestOutcome> CallProviderAsync(
        Guid userId,
        SuggestionRequest request,
        Guid reservationId,
        CancellationToken cancellationToken)
    {
        using var deadline = new CancellationTokenSource(options.ServerDeadline, timeProvider);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, deadline.Token);

        SuggestionResult result;
        try
        {
            // WaitAsync hält die Frist auch dann ein, wenn ein Adapter die Abbruchanforderung ignoriert.
            result = await provider.GenerateAsync(request, linked.Token).WaitAsync(linked.Token);
        }
        catch (OperationCanceledException)
        {
            return cancellationToken.IsCancellationRequested
                ? Denied(RequestDenial.Cancelled, SuggestionMessages.Cancelled)
                : Denied(RequestDenial.TimedOut, SuggestionMessages.TimedOut);
        }
        catch (Exception)
        {
            return Denied(RequestDenial.ProviderFailed, SuggestionMessages.ProviderFailed);
        }

        switch (result)
        {
            case SuggestionResult.Failure failure:
                await budget.SettleAsync(reservationId, failure.Cost);
                return failure.Kind == AiFailureKind.InvalidResponse
                    ? Denied(RequestDenial.InvalidResponse, SuggestionMessages.InvalidResponse)
                    : Denied(RequestDenial.ProviderFailed, SuggestionMessages.ProviderFailed);

            case SuggestionResult.Success success:
                await budget.SettleAsync(reservationId, success.Cost);
                var suggestion = new AiSuggestion(
                    Guid.NewGuid(),
                    userId,
                    request.Parameters,
                    success.Draft,
                    provider.ProviderName,
                    timeProvider.GetUtcNow());
                suggestions.Add(suggestion);
                await unitOfWork.SaveChangesAsync(CancellationToken.None);
                return new RequestOutcome.Created(suggestion);

            default:
                throw new InvalidOperationException($"Unbekanntes Ergebnis: {result.GetType().Name}");
        }
    }

    private async Task<AiSuggestion?> FindPendingAsync(Guid userId, Guid suggestionId, CancellationToken cancellationToken)
    {
        var suggestion = await suggestions.FindAsync(suggestionId, cancellationToken);
        return suggestion is { Status: SuggestionStatus.Pending } && suggestion.UserId == userId ? suggestion : null;
    }

    private static RequestOutcome.Denied Denied(RequestDenial reason, string message) => new(reason, message);
}
