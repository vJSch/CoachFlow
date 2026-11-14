using System.Security.Claims;
using CoachFlow.Api.Accounts;
using CoachFlow.Api.Data;
using CoachFlow.Api.Exercises;
using CoachFlow.Api.Plans;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CoachFlow.Api.Suggestions;

public record SuggestionRequest(int? ParticipantCount, Level? Level, Spread? Spread, int? DurationMinutes, List<Guid>? FocusIds);

public record SuggestionResponse(IReadOnlyList<ExerciseSuggestion> Suggestions, bool IsTestProvider);

public record SuggestionStatusResponse(bool Enabled, bool IsTestProvider);

public static class SuggestionEndpoints
{
    // Wortgleich mit der Meldung der Oberfläche nach 15 Sekunden.
    public const string TimeoutMessage = "Es konnte rechtzeitig kein Übungsvorschlag erstellt werden. Deine Eingaben bleiben erhalten.";

    public static void MapCoachFlowSuggestions(this IEndpointRouteBuilder api)
    {
        var suggestions = api.MapGroup("/exercise-suggestions").RequireAuthorization();
        suggestions.MapGet("/status", Status);
        suggestions.MapPost("", SuggestAsync);
    }

    private static IResult Status(IOptions<AiOptions> options) => Results.Ok(new SuggestionStatusResponse(
        options.Value.Provider != SuggestionProviders.None,
        options.Value.Provider == SuggestionProviders.Test));

    private static async Task<IResult> SuggestAsync(
        SuggestionRequest request,
        HttpContext http,
        ClaimsPrincipal principal,
        UserManager<AppUser> users,
        AppDbContext db,
        IOptions<AiOptions> options,
        SuggestionLimiter limiter,
        IExerciseSuggestionProvider provider,
        TimeProvider time,
        ILogger<SuggestionLimiter> logger)
    {
        var ai = options.Value;
        if (ai.Provider == SuggestionProviders.None)
        {
            return Problem(StatusCodes.Status503ServiceUnavailable, "KI-Vorschläge sind noch nicht eingerichtet.");
        }

        var (input, errors) = await BuildInputAsync(request, db, ai.SuggestionCount);
        if (input is null)
        {
            return Results.ValidationProblem(errors, detail: "Bitte prüfe die Angaben des Plans.");
        }

        var userId = users.GetUserId(principal)!;
        if (ai.MonthlyBudgetEuro is { } budget && await SpentThisMonthAsync(db, time) >= budget)
        {
            return Problem(StatusCodes.Status429TooManyRequests,
                "Das KI-Budget für diesen Monat ist aufgebraucht. Ab dem nächsten Monat sind wieder Vorschläge möglich.");
        }

        switch (limiter.TryStart(userId, out var lease, out var retryAfter))
        {
            case LimitRejection.AlreadyRunning:
                return Problem(StatusCodes.Status429TooManyRequests,
                    "Es läuft bereits eine Anfrage. Bitte warte, bis sie fertig ist, oder brich sie ab.");
            case LimitRejection.PerMinute:
                var seconds = Math.Max(1, (int)Math.Ceiling(retryAfter.TotalSeconds));
                http.Response.Headers.RetryAfter = seconds.ToString();
                return Problem(StatusCodes.Status429TooManyRequests,
                    $"Du hast in der letzten Minute schon {ai.RequestsPerMinute} Vorschläge angefordert. Bitte warte noch {seconds} Sekunden.");
        }

        using (lease)
        {
            var usage = new AiUsage
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                CreatedAt = time.GetUtcNow().UtcDateTime,
                Provider = ai.Provider
            };

            using var timeout = new CancellationTokenSource(ai.Timeout);
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(timeout.Token, http.RequestAborted);
            IResult result;
            try
            {
                var answer = await provider.SuggestAsync(input, linked.Token);
                usage.InputTokens = answer.InputTokens;
                usage.OutputTokens = answer.OutputTokens;
                usage.EstimatedCostEuro = answer.EstimatedCostEuro;

                var parsed = SuggestionParser.Parse(answer.Json, ai.SuggestionCount);
                usage.Result = parsed.Count > 0 ? AiUsageResult.Success : AiUsageResult.InvalidResponse;
                result = parsed.Count > 0
                    ? Results.Ok(new SuggestionResponse(parsed, ai.Provider == SuggestionProviders.Test))
                    : Problem(StatusCodes.Status502BadGateway,
                        "Die KI hat keine brauchbare Antwort geliefert. Bitte versuche es erneut. Deine Eingaben bleiben erhalten.");
            }
            catch (OperationCanceledException) when (http.RequestAborted.IsCancellationRequested)
            {
                // Abbruch in der App. Eine Antwort liest niemand mehr.
                usage.Result = AiUsageResult.Cancelled;
                result = Results.Empty;
            }
            catch (OperationCanceledException) when (timeout.IsCancellationRequested)
            {
                usage.Result = AiUsageResult.TimedOut;
                result = Problem(StatusCodes.Status504GatewayTimeout, TimeoutMessage);
            }
            catch (SuggestionProviderException ex)
            {
                logger.LogWarning(ex, "KI-Anbieter meldet einen Fehler ({Failure}).", ex.Failure);
                usage.Result = AiUsageResult.ProviderError;
                result = ex.Failure == ProviderFailure.Overloaded
                    ? Problem(StatusCodes.Status503ServiceUnavailable,
                        "Der KI-Dienst ist gerade ausgelastet. Bitte versuche es in einer Minute erneut.")
                    : Problem(StatusCodes.Status502BadGateway,
                        "Der KI-Dienst ist gerade nicht erreichbar. Bitte versuche es später erneut. Deine Eingaben bleiben erhalten.");
            }

            // Auch abgebrochene und fehlgeschlagene Anfragen werden erfasst, sie können Kosten verursachen.
            db.AiUsages.Add(usage);
            await db.SaveChangesAsync(CancellationToken.None);
            return result;
        }
    }

    private static async Task<(ProviderInput? Input, Dictionary<string, string[]> Errors)> BuildInputAsync(
        SuggestionRequest request, AppDbContext db, int count)
    {
        var errors = new Dictionary<string, string[]>();
        if (request.ParticipantCount is not (>= 1 and <= ExerciseEndpoints.MaxParticipants))
        {
            errors["participantCount"] = [$"Die Teilnehmerzahl muss zwischen 1 und {ExerciseEndpoints.MaxParticipants} liegen."];
        }

        if (request.Level is null)
        {
            errors["level"] = ["Bitte wähle ein Niveau."];
        }

        if (request.Spread is null)
        {
            errors["spread"] = ["Bitte wähle die Streuung."];
        }

        if (request.DurationMinutes is not (>= 1 and <= PlanEndpoints.MaxDurationMinutes))
        {
            errors["durationMinutes"] = [$"Die Trainingsdauer muss zwischen 1 und {PlanEndpoints.MaxDurationMinutes} Minuten liegen."];
        }

        var focusIds = request.FocusIds?.Distinct().ToList() ?? [];
        var focusNames = new List<string>();
        if (focusIds.Count > TrainingPlan.MaxFocuses)
        {
            errors["focusIds"] = [$"Ein Plan hat höchstens {TrainingPlan.MaxFocuses} Schwerpunkte."];
        }
        else if (focusIds.Count > 0)
        {
            var focuses = await db.Focuses.Where(f => focusIds.Contains(f.Id)).ToListAsync();
            if (focuses.Count != focusIds.Count)
            {
                errors["focusIds"] = ["Mindestens ein Schwerpunkt existiert nicht mehr. Bitte lade die Seite neu."];
            }

            focusNames = focuses.Select(f => f.Name).OrderBy(n => n, Text.GermanOrder).ToList();
        }

        if (errors.Count > 0)
        {
            return (null, errors);
        }

        return (new ProviderInput(
            request.ParticipantCount!.Value,
            request.Level!.Value,
            request.Spread!.Value,
            request.DurationMinutes!.Value,
            focusNames,
            count), errors);
    }

    private static async Task<decimal> SpentThisMonthAsync(AppDbContext db, TimeProvider time)
    {
        var now = time.GetUtcNow().UtcDateTime;
        var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        // SQLite kann decimal nicht summieren. Bei wenigen Anfragen im Monat ist das Summieren hier unproblematisch.
        var costs = await db.AiUsages.Where(u => u.CreatedAt >= monthStart).Select(u => u.EstimatedCostEuro).ToListAsync();
        return costs.Sum();
    }

    private static IResult Problem(int status, string detail) => Results.Problem(detail: detail, statusCode: status);
}
