using System.Security.Claims;
using CoachFlow.Api.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CoachFlow.Api.Exercises;

public record FocusResponse(Guid Id, string Name);

public record CreateFocusRequest(string? Name);

public record ExerciseRequest(
    string? Name,
    string? Description,
    int? DefaultDurationMinutes,
    int? MinParticipants,
    int? MaxParticipants,
    Level? Level,
    ExerciseOrigin? Origin,
    List<Guid>? FocusIds,
    int? Version);

public record ExerciseResponse(
    Guid Id,
    string Name,
    string Description,
    int DefaultDurationMinutes,
    int? MinParticipants,
    int? MaxParticipants,
    Level? Level,
    ExerciseOrigin Origin,
    IReadOnlyList<FocusResponse> Focuses,
    int Version,
    string? UpdatedBy,
    DateTime UpdatedAt);

public record ExerciseQuery(string? Search, Level? Level, int? Participants, Guid[]? FocusId);

public static class ExerciseEndpoints
{
    public const int MaxNameLength = 120;
    public const int MaxDescriptionLength = 4000;
    public const int MaxFocusNameLength = 60;
    public const int MaxDurationMinutes = 180;
    public const int MaxParticipants = 100;

    public static void MapCoachFlowExercises(this IEndpointRouteBuilder api)
    {
        var focuses = api.MapGroup("/focuses").RequireAuthorization();
        focuses.MapGet("", ListFocusesAsync);
        focuses.MapPost("", CreateFocusAsync);

        var exercises = api.MapGroup("/exercises").RequireAuthorization();
        exercises.MapGet("", ListExercisesAsync);
        exercises.MapGet("/{id:guid}", GetExerciseAsync);
        exercises.MapPost("", CreateExerciseAsync);
        exercises.MapPut("/{id:guid}", UpdateExerciseAsync);
        exercises.MapDelete("/{id:guid}", DeleteExerciseAsync);
    }

    private static async Task<IResult> ListFocusesAsync(AppDbContext db)
    {
        var all = await db.Focuses.Where(f => f.Sport == Sports.Volleyball).ToListAsync();
        return Results.Ok(all.OrderBy(f => f.Name, Text.GermanOrder).Select(ToResponse));
    }

    private static async Task<IResult> CreateFocusAsync(
        CreateFocusRequest request, AppDbContext db, ClaimsPrincipal principal, UserManager<Accounts.AppUser> users, TimeProvider time)
    {
        var name = Text.Clean(request.Name);
        if (name.Length is 0 or > MaxFocusNameLength)
        {
            return Validation("name", $"Bitte gib eine Bezeichnung mit höchstens {MaxFocusNameLength} Zeichen an.");
        }

        var normalized = Text.Normalize(name);
        var existing = await db.Focuses.FirstOrDefaultAsync(f => f.Sport == Sports.Volleyball && f.NormalizedName == normalized);
        if (existing is not null)
        {
            return Results.Problem(
                detail: $"Den Schwerpunkt „{existing.Name}“ gibt es bereits.",
                statusCode: StatusCodes.Status409Conflict,
                extensions: new Dictionary<string, object?> { ["focus"] = ToResponse(existing) });
        }

        var focus = new Focus
        {
            Id = Guid.NewGuid(),
            Name = name,
            NormalizedName = normalized,
            CreatedById = users.GetUserId(principal),
            CreatedAt = time.GetUtcNow().UtcDateTime
        };
        db.Focuses.Add(focus);
        await db.SaveChangesAsync();
        return Results.Created($"/api/focuses/{focus.Id}", ToResponse(focus));
    }

    private static async Task<IResult> ListExercisesAsync([AsParameters] ExerciseQuery query, AppDbContext db)
    {
        var exercises = WithDetails(db).Where(e => e.Sport == Sports.Volleyball);

        var search = Text.Normalize(query.Search);
        if (search.Length > 0)
        {
            exercises = exercises.Where(e => e.SearchText.Contains(search));
        }

        // Übungen ohne Niveau oder ohne Grenzen passen zu jeder Auswahl.
        if (query.Level is { } level)
        {
            exercises = exercises.Where(e => e.Level == null || e.Level == level);
        }

        if (query.Participants is { } participants)
        {
            exercises = exercises.Where(e =>
                (e.MinParticipants == null || e.MinParticipants <= participants)
                && (e.MaxParticipants == null || e.MaxParticipants >= participants));
        }

        if (query.FocusId is { Length: > 0 } focusIds)
        {
            exercises = exercises.Where(e => e.Focuses.Any(f => focusIds.Contains(f.Id)));
        }

        var result = await exercises.ToListAsync();
        return Results.Ok(result.OrderBy(e => e.Name, Text.GermanOrder).Select(ToResponse));
    }

    private static async Task<IResult> GetExerciseAsync(Guid id, AppDbContext db)
    {
        var exercise = await WithDetails(db).FirstOrDefaultAsync(e => e.Id == id);
        return exercise is null ? NotFound() : Results.Ok(ToResponse(exercise));
    }

    private static async Task<IResult> CreateExerciseAsync(
        ExerciseRequest request, AppDbContext db, ClaimsPrincipal principal, UserManager<Accounts.AppUser> users, TimeProvider time)
    {
        var focuses = await LoadFocusesAsync(db, request.FocusIds);
        var errors = Validate(request, focuses, requireVersion: false);
        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors, detail: "Bitte prüfe die Eingaben.");
        }

        var exercise = new Exercise
        {
            Id = Guid.NewGuid(),
            Origin = request.Origin ?? ExerciseOrigin.Own,
            Version = 1
        };
        Apply(exercise, request, focuses, users.GetUserId(principal), time);
        db.Exercises.Add(exercise);
        await db.SaveChangesAsync();

        return Results.Created($"/api/exercises/{exercise.Id}", ToResponse(await ReloadAsync(db, exercise.Id)));
    }

    private static async Task<IResult> UpdateExerciseAsync(
        Guid id, ExerciseRequest request, AppDbContext db, ClaimsPrincipal principal, UserManager<Accounts.AppUser> users, TimeProvider time)
    {
        var exercise = await WithDetails(db).FirstOrDefaultAsync(e => e.Id == id);
        if (exercise is null)
        {
            return NotFound();
        }

        var focuses = await LoadFocusesAsync(db, request.FocusIds);
        var errors = Validate(request, focuses, requireVersion: true);
        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors, detail: "Bitte prüfe die Eingaben.");
        }

        if (request.Version != exercise.Version)
        {
            return Conflict(exercise);
        }

        Apply(exercise, request, focuses, users.GetUserId(principal), time);
        exercise.Version++;
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

    private static async Task<IResult> DeleteExerciseAsync(Guid id, AppDbContext db)
    {
        // Pläne behalten ihre Kopie der Übung, deshalb wird wirklich gelöscht.
        var deleted = await db.Exercises.Where(e => e.Id == id).ExecuteDeleteAsync();
        return deleted == 0 ? NotFound() : Results.NoContent();
    }

    private static Dictionary<string, string[]> Validate(ExerciseRequest request, List<Focus> focuses, bool requireVersion)
    {
        var errors = new Dictionary<string, string[]>();
        void Add(string field, string message) => errors[field] = [message];

        var name = Text.Clean(request.Name);
        if (name.Length is 0 or > MaxNameLength)
        {
            Add("name", $"Bitte gib einen Namen mit höchstens {MaxNameLength} Zeichen an.");
        }

        if ((request.Description?.Trim().Length ?? 0) > MaxDescriptionLength)
        {
            Add("description", $"Die Beschreibung darf höchstens {MaxDescriptionLength} Zeichen lang sein.");
        }

        if (request.DefaultDurationMinutes is not (>= 1 and <= MaxDurationMinutes))
        {
            Add("defaultDurationMinutes", $"Die Dauer muss zwischen 1 und {MaxDurationMinutes} Minuten liegen.");
        }

        if (request.MinParticipants is < 1 or > MaxParticipants || request.MaxParticipants is < 1 or > MaxParticipants)
        {
            Add("participants", $"Die Teilnehmerzahl muss zwischen 1 und {MaxParticipants} liegen.");
        }
        else if (request.MinParticipants > request.MaxParticipants)
        {
            Add("participants", "Die Mindestzahl darf nicht größer als die Höchstzahl sein.");
        }

        if (focuses.Count != (request.FocusIds?.Distinct().Count() ?? 0))
        {
            Add("focusIds", "Mindestens ein Schwerpunkt existiert nicht mehr. Bitte lade die Seite neu.");
        }

        if (requireVersion && request.Version is null)
        {
            Add("version", "Die Version fehlt.");
        }

        return errors;
    }

    private static void Apply(Exercise exercise, ExerciseRequest request, List<Focus> focuses, string? userId, TimeProvider time)
    {
        exercise.Name = Text.Clean(request.Name);
        exercise.Description = request.Description?.Trim() ?? "";
        exercise.DefaultDurationMinutes = request.DefaultDurationMinutes!.Value;
        exercise.MinParticipants = request.MinParticipants;
        exercise.MaxParticipants = request.MaxParticipants;
        exercise.Level = request.Level;
        exercise.Focuses = focuses;
        exercise.SearchText = Text.Normalize(exercise.Name + " " + exercise.Description);
        exercise.UpdatedById = userId;
        exercise.UpdatedAt = time.GetUtcNow().UtcDateTime;
    }

    private static async Task<List<Focus>> LoadFocusesAsync(AppDbContext db, List<Guid>? ids)
    {
        if (ids is not { Count: > 0 })
        {
            return [];
        }

        var distinct = ids.Distinct().ToList();
        return await db.Focuses.Where(f => distinct.Contains(f.Id)).ToListAsync();
    }

    private static IQueryable<Exercise> WithDetails(AppDbContext db) =>
        db.Exercises.Include(e => e.Focuses).Include(e => e.UpdatedBy);

    private static async Task<Exercise> ReloadAsync(AppDbContext db, Guid id)
    {
        db.ChangeTracker.Clear();
        return await WithDetails(db).AsNoTracking().FirstAsync(e => e.Id == id);
    }

    private static IResult Conflict(Exercise current) => Results.Problem(
        detail: $"Diese Übung wurde inzwischen von {current.UpdatedBy?.Name ?? "jemand anderem"} geändert. "
            + "Lade den aktuellen Stand, bevor du weiterarbeitest.",
        statusCode: StatusCodes.Status409Conflict,
        extensions: new Dictionary<string, object?> { ["current"] = ToResponse(current) });

    private static IResult NotFound() => Results.Problem(
        detail: "Diese Übung gibt es nicht mehr. Vielleicht wurde sie gelöscht.",
        statusCode: StatusCodes.Status404NotFound);

    private static IResult Validation(string field, string message) =>
        Results.ValidationProblem(new Dictionary<string, string[]> { [field] = [message] }, detail: message);

    public static FocusResponse ToResponse(Focus focus) => new(focus.Id, focus.Name);

    private static ExerciseResponse ToResponse(Exercise e) => new(
        e.Id,
        e.Name,
        e.Description,
        e.DefaultDurationMinutes,
        e.MinParticipants,
        e.MaxParticipants,
        e.Level,
        e.Origin,
        e.Focuses.OrderBy(f => f.Name, Text.GermanOrder).Select(ToResponse).ToList(),
        e.Version,
        e.UpdatedBy?.Name,
        e.UpdatedAt);
}
