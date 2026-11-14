using System.Security.Claims;
using CoachFlow.Application;
using CoachFlow.Application.Ai;
using CoachFlow.Application.Exercises;
using CoachFlow.Application.Plans;
using CoachFlow.Application.Suggestions;
using CoachFlow.Domain;

namespace CoachFlow.Api;

public sealed record UpdateExerciseBody(int Version, ExerciseContent Content);
public sealed record AddFocusAreaBody(string Name);
public sealed record CreatePlanBody(string Title, TrainingParameters Parameters, IReadOnlyList<PlanItem> Items, Guid? ConfirmDeletionOf);
public sealed record UpdatePlanBody(int Version, string Title, TrainingParameters Parameters, IReadOnlyList<PlanItem> Items);
public sealed record RequestSuggestionBody(TrainingParameters Parameters);
public sealed record EditSuggestionBody(ExerciseContent Draft);

/// <summary>Maschinenlesbarer Grund plus fertiger Meldungstext für die Oberfläche.</summary>
public sealed record ApiError(string Code, string Message, IReadOnlyList<string>? Errors = null);

public sealed record PlanLimitWarning(string Code, string Message, StoredPlanInfo PlanToDelete);

public sealed record AdoptBlockedResponse(string Code, string Message, IReadOnlyList<Finding> Findings);

internal static class Endpoints
{
    public static void MapCoachFlowEndpoints(this WebApplication app)
    {
        var api = app.MapGroup("/api").RequireAuthorization();
        MapFocusAreas(api.MapGroup("/focus-areas"));
        MapExercises(api.MapGroup("/exercises"));
        MapPlans(api.MapGroup("/plans"));
        MapSuggestions(api.MapGroup("/suggestions"));
    }

    private static Guid UserId(this ClaimsPrincipal user) =>
        Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new InvalidOperationException("Angemeldete Person ohne Id."));

    private static void MapFocusAreas(RouteGroupBuilder group)
    {
        group.MapGet("/", (ExerciseService service, CancellationToken ct) => service.ListFocusAreasAsync(ct));

        group.MapPost("/", async (AddFocusAreaBody body, ExerciseService service, CancellationToken ct) =>
            ToResult(await service.AddFocusAreaAsync(body.Name, ct), created: true));
    }

    private static void MapExercises(RouteGroupBuilder group)
    {
        group.MapGet("/", (ExerciseService service, CancellationToken ct) => service.ListAsync(ct));

        group.MapPost("/", async (ExerciseContent body, ClaimsPrincipal user, ExerciseService service, CancellationToken ct) =>
            ToResult(await service.CreateAsync(user.UserId(), body, ct), created: true));

        group.MapPut("/{id:guid}", async (Guid id, UpdateExerciseBody body, ExerciseService service, CancellationToken ct) =>
            ToResult(await service.UpdateAsync(id, body.Version, body.Content, ct)));

        group.MapDelete("/{id:guid}", async (Guid id, int version, ExerciseService service, CancellationToken ct) =>
            ToResult(await service.ArchiveAsync(id, version, ct)));
    }

    private static void MapPlans(RouteGroupBuilder group)
    {
        group.MapGet("/", (ClaimsPrincipal user, PlanService service, CancellationToken ct) => service.ListAsync(user.UserId(), ct));

        group.MapGet("/{id:guid}", async (Guid id, ClaimsPrincipal user, PlanService service, CancellationToken ct) =>
            await service.GetAsync(user.UserId(), id, ct) is { } plan ? Results.Ok(plan) : NotFound());

        group.MapGet("/{id:guid}/export", async (
            Guid id, ClaimsPrincipal user, PlanService plans, ExerciseService exercises, IPlanExporter exporter, CancellationToken ct) =>
        {
            if (await plans.GetAsync(user.UserId(), id, ct) is not { } plan) return NotFound();

            var buffer = new MemoryStream();
            exporter.Export(plan, await exercises.ListFocusAreasAsync(ct), buffer);
            return Results.File(buffer.ToArray(), exporter.ContentType, ExportFileName(plan.Title, exporter.FileExtension));
        });

        group.MapPost("/", async (CreatePlanBody body, ClaimsPrincipal user, PlanService service, CancellationToken ct) =>
            await service.CreateAsync(user.UserId(), new PlanInput(body.Title, body.Parameters, body.Items), body.ConfirmDeletionOf, ct) switch
            {
                CreatePlanOutcome.Created created => Results.Created($"/api/plans/{created.Plan.Id}", created.Plan),
                CreatePlanOutcome.Invalid invalid => Invalid(invalid.Errors),
                CreatePlanOutcome.ConfirmationRequired warning => Results.Conflict(
                    new PlanLimitWarning("plan-limit-confirmation", warning.Message, warning.PlanToDelete)),
                var other => throw new InvalidOperationException(other.GetType().Name)
            });

        group.MapPut("/{id:guid}", async (Guid id, UpdatePlanBody body, ClaimsPrincipal user, PlanService service, CancellationToken ct) =>
            ToResult(await service.UpdateAsync(user.UserId(), id, body.Version, new PlanInput(body.Title, body.Parameters, body.Items), ct)));

        group.MapDelete("/{id:guid}", async (Guid id, ClaimsPrincipal user, PlanService service, CancellationToken ct) =>
            await service.DeleteAsync(user.UserId(), id, ct) ? Results.NoContent() : NotFound());
    }

    private static void MapSuggestions(RouteGroupBuilder group)
    {
        group.MapPost("/", async (
            RequestSuggestionBody body,
            ClaimsPrincipal user,
            SuggestionService suggestions,
            ExerciseService exercises,
            CancellationToken ct) =>
        {
            // Namen kommen aus dem Bestand, nicht vom Client: Nur bekannte Schwerpunkte gelangen zur KI.
            var known = (await exercises.ListFocusAreasAsync(ct)).ToDictionary(f => f.Id, f => f.Name);
            if (body.Parameters.FocusAreaIds.Any(id => !known.ContainsKey(id)))
                return Invalid(["Ein gewählter Trainingsschwerpunkt existiert nicht."]);

            var request = new SuggestionRequest(body.Parameters, body.Parameters.FocusAreaIds.Select(id => known[id]).ToList());
            return await suggestions.RequestAsync(user.UserId(), request, ct) switch
            {
                RequestOutcome.Created created => Results.Created($"/api/suggestions/{created.Suggestion.Id}", created.Suggestion),
                RequestOutcome.Denied denied => Denied(denied),
                var other => throw new InvalidOperationException(other.GetType().Name)
            };
        });

        group.MapPut("/{id:guid}", async (Guid id, EditSuggestionBody body, ClaimsPrincipal user, SuggestionService service, CancellationToken ct) =>
            await service.EditAsync(user.UserId(), id, body.Draft, ct) is { } findings ? Results.Ok(findings) : NotFound());

        group.MapPost("/{id:guid}/adopt", async (Guid id, ClaimsPrincipal user, SuggestionService service, CancellationToken ct) =>
            await service.AdoptAsync(user.UserId(), id, ct) switch
            {
                AdoptOutcome.Adopted adopted => Results.Ok(adopted.Exercise),
                AdoptOutcome.Blocked blocked => Results.UnprocessableEntity(new AdoptBlockedResponse(
                    "suggestion-blocked", "Der Vorschlag kann noch nicht übernommen werden. Bitte korrigiere die markierten Punkte.", blocked.Findings)),
                _ => NotFound()
            });

        group.MapPost("/{id:guid}/discard", async (Guid id, ClaimsPrincipal user, SuggestionService service, CancellationToken ct) =>
            await service.DiscardAsync(user.UserId(), id, ct) ? Results.NoContent() : NotFound());
    }

    /// <summary>Plantitel als Dateiname: ohne Zeichen, die Windows, macOS oder Pfade stören.</summary>
    internal static string ExportFileName(string title, string extension)
    {
        var invalid = Path.GetInvalidFileNameChars().Concat(['/', '\\', ':', '*', '?', '"', '<', '>', '|']).ToHashSet();
        var cleaned = new string(title.Select(c => invalid.Contains(c) || char.IsControl(c) ? '_' : c).ToArray()).Trim(' ', '.');
        if (cleaned.Length > 80) cleaned = cleaned[..80].TrimEnd();
        return $"CoachFlow - {(cleaned.Length == 0 ? "Plan" : cleaned)}{extension}";
    }

    private static IResult Denied(RequestOutcome.Denied denied)
    {
        var status = denied.Reason switch
        {
            RequestDenial.InvalidInput => StatusCodes.Status400BadRequest,
            RequestDenial.AlreadyRunning or RequestDenial.RateLimited => StatusCodes.Status429TooManyRequests,
            RequestDenial.TimedOut => StatusCodes.Status504GatewayTimeout,
            RequestDenial.Cancelled => 499,
            RequestDenial.InvalidResponse => StatusCodes.Status502BadGateway,
            _ => StatusCodes.Status503ServiceUnavailable
        };
        var code = denied.Reason.ToString();
        return Results.Json(new ApiError(char.ToLowerInvariant(code[0]) + code[1..], denied.Message), statusCode: status);
    }

    private static IResult ToResult<T>(ChangeOutcome<T> outcome, bool created = false) => outcome switch
    {
        ChangeOutcome<T>.Done done => created ? Results.Json(done.Value, statusCode: StatusCodes.Status201Created) : Results.Ok(done.Value),
        ChangeOutcome<T>.Invalid invalid => Invalid(invalid.Errors),
        ChangeOutcome<T>.Conflict => Results.Conflict(new ApiError("version-conflict", ConflictMessages.Changed)),
        _ => NotFound()
    };

    private static IResult Invalid(IReadOnlyList<string> errors) =>
        Results.BadRequest(new ApiError("invalid", "Bitte prüfe deine Eingaben.", errors));

    private static IResult NotFound() =>
        Results.NotFound(new ApiError("not-found", "Der Eintrag wurde nicht gefunden."));
}
