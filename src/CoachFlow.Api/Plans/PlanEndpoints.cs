using System.Security.Claims;
using CoachFlow.Api.Accounts;
using CoachFlow.Api.Data;
using CoachFlow.Api.Exercises;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CoachFlow.Api.Plans;

// Id gesetzt: bestehende Position, ihr kopierter Text bleibt. Id leer: neue Position aus ExerciseId.
public record PositionRequest(Guid? Id, Guid? ExerciseId, int? PlannedDurationMinutes, string? Note);

public record PlanRequest(
    string? Title,
    int? ParticipantCount,
    Level? Level,
    Spread? Spread,
    int? DurationMinutes,
    string? Note,
    List<Guid>? FocusIds,
    List<PositionRequest>? Positions,
    int? Version);

public record PositionResponse(Guid Id, Guid? ExerciseId, string Name, string Description, int PlannedDurationMinutes, string Note);

public record PlanResponse(
    Guid Id,
    string Title,
    int ParticipantCount,
    Level Level,
    Spread Spread,
    int DurationMinutes,
    string Note,
    IReadOnlyList<FocusResponse> Focuses,
    IReadOnlyList<PositionResponse> Positions,
    int TotalPositionMinutes,
    IReadOnlyList<string> Warnings,
    int Version,
    string? UpdatedBy,
    DateTime UpdatedAt);

public static class PlanEndpoints
{
    public const int MaxTitleLength = 120;
    public const int MaxNoteLength = 2000;
    public const int MaxPositionNoteLength = 1000;
    public const int MaxDurationMinutes = 300;
    public const int MaxPositions = 50;

    public static void MapCoachFlowPlans(this IEndpointRouteBuilder api)
    {
        var plans = api.MapGroup("/plans").RequireAuthorization();
        plans.MapGet("", ListPlansAsync);
        plans.MapGet("/{id:guid}", GetPlanAsync);
        plans.MapPost("", CreatePlanAsync);
        plans.MapPut("/{id:guid}", UpdatePlanAsync);
        plans.MapDelete("/{id:guid}", DeletePlanAsync);
    }

    // Vollständige Pläne, weil die App daraus später auch die Offline-Lesekopie bildet.
    private static async Task<IResult> ListPlansAsync(AppDbContext db)
    {
        var plans = await WithDetails(db).AsNoTracking().Where(p => p.Sport == Sports.Volleyball).ToListAsync();
        return Results.Ok(plans.OrderByDescending(p => p.UpdatedAt).Select(ToResponse));
    }

    private static async Task<IResult> GetPlanAsync(Guid id, AppDbContext db)
    {
        var plan = await WithDetails(db).AsNoTracking().FirstOrDefaultAsync(p => p.Id == id);
        return plan is null ? NotFound() : Results.Ok(ToResponse(plan));
    }

    private static async Task<IResult> CreatePlanAsync(
        PlanRequest request, AppDbContext db, ClaimsPrincipal principal, UserManager<AppUser> users, TimeProvider time)
    {
        var plan = new TrainingPlan { Id = Guid.NewGuid(), Version = 1 };
        var errors = await ApplyAsync(plan, request, db, requireVersion: false);
        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors, detail: "Bitte prüfe die Eingaben.");
        }

        Touch(plan, users.GetUserId(principal), time);
        db.TrainingPlans.Add(plan);
        await db.SaveChangesAsync();
        return Results.Created($"/api/plans/{plan.Id}", ToResponse(await ReloadAsync(db, plan.Id)));
    }

    private static async Task<IResult> UpdatePlanAsync(
        Guid id, PlanRequest request, AppDbContext db, ClaimsPrincipal principal, UserManager<AppUser> users, TimeProvider time)
    {
        var plan = await WithDetails(db).FirstOrDefaultAsync(p => p.Id == id);
        if (plan is null)
        {
            return NotFound();
        }

        // Erst die Version prüfen: Bei einem Konflikt sind Prüffehler gegen einen veralteten Stand wenig hilfreich.
        if (request.Version is { } version && version != plan.Version)
        {
            return Conflict(plan);
        }

        var errors = await ApplyAsync(plan, request, db, requireVersion: true);
        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors, detail: "Bitte prüfe die Eingaben.");
        }

        Touch(plan, users.GetUserId(principal), time);
        plan.Version++;
        // Version ist ein Concurrency-Token: Hat jemand zwischen Laden und Speichern geändert, schlägt das Speichern fehl.
        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            return Conflict(await ReloadAsync(db, id));
        }

        return Results.Ok(ToResponse(await ReloadAsync(db, id)));
    }

    private static async Task<IResult> DeletePlanAsync(Guid id, AppDbContext db)
    {
        var deleted = await db.TrainingPlans.Where(p => p.Id == id).ExecuteDeleteAsync();
        return deleted == 0 ? NotFound() : Results.NoContent();
    }

    // Prüft die Anfrage und überträgt sie in den Plan. Bei Fehlern speichert der Aufrufer nichts.
    private static async Task<Dictionary<string, string[]>> ApplyAsync(
        TrainingPlan plan, PlanRequest request, AppDbContext db, bool requireVersion)
    {
        var errors = new Dictionary<string, string[]>();
        void Add(string field, string message) => errors[field] = [message];

        var title = Text.Clean(request.Title);
        if (title.Length is 0 or > MaxTitleLength)
        {
            Add("title", $"Bitte gib einen Titel mit höchstens {MaxTitleLength} Zeichen an.");
        }

        if (request.ParticipantCount is not (>= 1 and <= ExerciseEndpoints.MaxParticipants))
        {
            Add("participantCount", $"Die Teilnehmerzahl muss zwischen 1 und {ExerciseEndpoints.MaxParticipants} liegen.");
        }

        if (request.Level is null)
        {
            Add("level", "Bitte wähle ein Niveau.");
        }

        if (request.Spread is null)
        {
            Add("spread", "Bitte wähle die Streuung.");
        }

        if (request.DurationMinutes is not (>= 1 and <= MaxDurationMinutes))
        {
            Add("durationMinutes", $"Die Trainingsdauer muss zwischen 1 und {MaxDurationMinutes} Minuten liegen.");
        }

        var note = request.Note?.Trim() ?? "";
        if (note.Length > MaxNoteLength)
        {
            Add("note", $"Die Notiz darf höchstens {MaxNoteLength} Zeichen lang sein.");
        }

        if (requireVersion && request.Version is null)
        {
            Add("version", "Die Version fehlt.");
        }

        var focusIds = request.FocusIds ?? [];
        var focuses = new List<Focus>();
        if (focusIds.Count != focusIds.Distinct().Count())
        {
            Add("focusIds", "Ein Schwerpunkt ist doppelt ausgewählt.");
        }
        else if (focusIds.Count > TrainingPlan.MaxFocuses)
        {
            Add("focusIds", $"Ein Plan hat höchstens {TrainingPlan.MaxFocuses} Schwerpunkte.");
        }
        else if (focusIds.Count > 0)
        {
            focuses = await db.Focuses.Where(f => focusIds.Contains(f.Id)).ToListAsync();
            if (focuses.Count != focusIds.Count)
            {
                Add("focusIds", "Mindestens ein Schwerpunkt existiert nicht mehr. Bitte lade die Seite neu.");
            }
        }

        var positions = await BuildPositionsAsync(plan, request.Positions ?? [], db, Add);

        if (errors.Count > 0)
        {
            return errors;
        }

        plan.Title = title;
        plan.ParticipantCount = request.ParticipantCount!.Value;
        plan.Level = request.Level!.Value;
        plan.Spread = request.Spread!.Value;
        plan.DurationMinutes = request.DurationMinutes!.Value;
        plan.Note = note;
        plan.Focuses = focuses;
        // Nicht mehr enthaltene Positionen fallen weg und werden über die Beziehung gelöscht.
        plan.Positions = positions;
        return errors;
    }

    private static async Task<List<PlanPosition>> BuildPositionsAsync(
        TrainingPlan plan, List<PositionRequest> requests, AppDbContext db, Action<string, string> addError)
    {
        if (requests.Count > MaxPositions)
        {
            addError("positions", $"Ein Plan hat höchstens {MaxPositions} Übungen.");
            return [];
        }

        var newExerciseIds = requests.Where(r => r.Id is null && r.ExerciseId is not null).Select(r => r.ExerciseId!.Value).Distinct().ToList();
        var exercises = await db.Exercises.AsNoTracking()
            .Where(e => newExerciseIds.Contains(e.Id))
            .ToDictionaryAsync(e => e.Id);

        var positions = new List<PlanPosition>();
        var usedIds = new HashSet<Guid>();
        for (var index = 0; index < requests.Count; index++)
        {
            var request = requests[index];
            var field = $"positions[{index}]";
            var number = index + 1;
            PlanPosition position;

            if (request.Id is { } existingId)
            {
                var existing = plan.Positions.FirstOrDefault(p => p.Id == existingId);
                if (existing is null || !usedIds.Add(existingId))
                {
                    addError(field, $"Übung {number} gehört nicht (mehr) zu diesem Plan. Bitte lade den Plan neu.");
                    continue;
                }

                position = existing;
            }
            else if (request.ExerciseId is { } exerciseId && exercises.TryGetValue(exerciseId, out var exercise))
            {
                // Hier entsteht die Kopie des Übungsstands. Die Id vergibt EF Core: Eine vorab gesetzte Id
                // hielte EF für eine bestehende Zeile und würde ändern statt einfügen.
                position = new PlanPosition
                {
                    ExerciseId = exercise.Id,
                    Name = exercise.Name,
                    Description = exercise.Description,
                    PlannedDurationMinutes = exercise.DefaultDurationMinutes
                };
            }
            else
            {
                addError(field, $"Übung {number} gibt es nicht mehr. Vielleicht wurde sie gelöscht.");
                continue;
            }

            var duration = request.PlannedDurationMinutes ?? position.PlannedDurationMinutes;
            if (duration is < 1 or > ExerciseEndpoints.MaxDurationMinutes)
            {
                addError(field, $"Die Dauer von Übung {number} muss zwischen 1 und {ExerciseEndpoints.MaxDurationMinutes} Minuten liegen.");
                continue;
            }

            var note = request.Note?.Trim() ?? "";
            if (note.Length > MaxPositionNoteLength)
            {
                addError(field, $"Die Notiz zu Übung {number} darf höchstens {MaxPositionNoteLength} Zeichen lang sein.");
                continue;
            }

            position.SortOrder = index;
            position.PlannedDurationMinutes = duration;
            position.Note = note;
            positions.Add(position);
        }

        return positions;
    }

    private static void Touch(TrainingPlan plan, string? userId, TimeProvider time)
    {
        plan.UpdatedById = userId;
        plan.UpdatedAt = time.GetUtcNow().UtcDateTime;
    }

    // Abweichungen der Dauer verhindern das Speichern nicht, sie werden nur gemeldet.
    public static IReadOnlyList<string> Warnings(TrainingPlan plan)
    {
        var total = plan.Positions.Sum(p => p.PlannedDurationMinutes);
        return plan.Positions.Count == 0 || total == plan.DurationMinutes
            ? []
            : [$"Die Übungen dauern zusammen {total} Minuten, geplant sind {plan.DurationMinutes} Minuten."];
    }

    private static IQueryable<TrainingPlan> WithDetails(AppDbContext db) =>
        db.TrainingPlans.Include(p => p.Focuses).Include(p => p.Positions).Include(p => p.UpdatedBy).AsSplitQuery();

    private static async Task<TrainingPlan> ReloadAsync(AppDbContext db, Guid id)
    {
        db.ChangeTracker.Clear();
        return await WithDetails(db).AsNoTracking().FirstAsync(p => p.Id == id);
    }

    private static IResult Conflict(TrainingPlan current) => Results.Problem(
        detail: $"Dieser Plan wurde inzwischen von {current.UpdatedBy?.Name ?? "jemand anderem"} geändert. "
            + "Lade den aktuellen Stand, bevor du weiterarbeitest.",
        statusCode: StatusCodes.Status409Conflict,
        extensions: new Dictionary<string, object?> { ["current"] = ToResponse(current) });

    private static IResult NotFound() => Results.Problem(
        detail: "Diesen Plan gibt es nicht mehr. Vielleicht wurde er gelöscht.",
        statusCode: StatusCodes.Status404NotFound);

    private static PlanResponse ToResponse(TrainingPlan plan) => new(
        plan.Id,
        plan.Title,
        plan.ParticipantCount,
        plan.Level,
        plan.Spread,
        plan.DurationMinutes,
        plan.Note,
        plan.Focuses.OrderBy(f => f.Name, Text.GermanOrder).Select(ExerciseEndpoints.ToResponse).ToList(),
        plan.Positions.OrderBy(p => p.SortOrder)
            .Select(p => new PositionResponse(p.Id, p.ExerciseId, p.Name, p.Description, p.PlannedDurationMinutes, p.Note))
            .ToList(),
        plan.Positions.Sum(p => p.PlannedDurationMinutes),
        Warnings(plan),
        plan.Version,
        plan.UpdatedBy?.Name,
        plan.UpdatedAt);
}
