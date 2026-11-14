using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using CoachFlow.Domain;

namespace CoachFlow.Client;

/// <summary>
/// Typisierter Zugriff auf die CoachFlow-API. Netzfehler werden zu <see cref="ApiStatus.Offline"/>,
/// statt als Ausnahme in der Oberfläche zu landen.
/// </summary>
public sealed class CoachFlowApi(HttpClient http, AppState state)
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    // --- Anmeldung -------------------------------------------------------------------------

    public async Task<ApiResult<AccountInfo>> GetAccountAsync(CancellationToken ct = default)
    {
        var result = await SendAsync<AccountInfo>(HttpMethod.Get, "api/auth/manage/info", null, ct);
        if (result.IsOk && result.Value is { } info) state.SignedIn(info.Email);
        return result;
    }

    public async Task<ApiResult<NoContent>> RegisterAsync(string email, string password, CancellationToken ct = default)
    {
        var result = await SendAsync<NoContent>(HttpMethod.Post, "api/auth/register", new { email, password }, ct);
        return result.Status == ApiStatus.Failed ? WithIdentityMessages(result) : result;
    }

    public async Task<ApiResult<NoContent>> LoginAsync(string email, string password, CancellationToken ct = default)
    {
        var result = await SendAsync<NoContent>(HttpMethod.Post, "api/auth/login?useCookies=true", new { email, password }, ct);
        // 401 heißt hier „falsche Daten oder gesperrt“. Die Meldung unterscheidet das bewusst nicht.
        if (result.Status == ApiStatus.Unauthorized)
            return ApiResult<NoContent>.Failed(new ApiError("login-failed", ClientMessages.LoginFailed), 401, null);
        if (result.IsOk) state.SignedIn(email);
        return result;
    }

    public async Task<ApiResult<NoContent>> LogoutAsync(CancellationToken ct = default)
    {
        var result = await SendAsync<NoContent>(HttpMethod.Post, "api/auth/logout", null, ct);
        if (result.IsOk || result.Status == ApiStatus.Unauthorized) state.SignedOut();
        return result;
    }

    // --- Schwerpunkte und Übungen ----------------------------------------------------------

    public Task<ApiResult<List<FocusAreaDto>>> ListFocusAreasAsync(CancellationToken ct = default) =>
        SendAsync<List<FocusAreaDto>>(HttpMethod.Get, "api/focus-areas", null, ct);

    public Task<ApiResult<FocusAreaDto>> AddFocusAreaAsync(string name, CancellationToken ct = default) =>
        SendAsync<FocusAreaDto>(HttpMethod.Post, "api/focus-areas", new { name }, ct);

    public Task<ApiResult<List<ExerciseDto>>> ListExercisesAsync(CancellationToken ct = default) =>
        SendAsync<List<ExerciseDto>>(HttpMethod.Get, "api/exercises", null, ct);

    public Task<ApiResult<ExerciseDto>> CreateExerciseAsync(ExerciseContent content, CancellationToken ct = default) =>
        SendAsync<ExerciseDto>(HttpMethod.Post, "api/exercises", content, ct);

    public Task<ApiResult<ExerciseDto>> UpdateExerciseAsync(Guid id, int version, ExerciseContent content, CancellationToken ct = default) =>
        SendAsync<ExerciseDto>(HttpMethod.Put, $"api/exercises/{id}", new { version, content }, ct);

    public Task<ApiResult<ExerciseDto>> ArchiveExerciseAsync(Guid id, int version, CancellationToken ct = default) =>
        SendAsync<ExerciseDto>(HttpMethod.Delete, $"api/exercises/{id}?version={version}", null, ct);

    // --- Pläne -----------------------------------------------------------------------------

    public Task<ApiResult<List<PlanDto>>> ListPlansAsync(CancellationToken ct = default) =>
        SendAsync<List<PlanDto>>(HttpMethod.Get, "api/plans", null, ct);

    public Task<ApiResult<PlanDto>> GetPlanAsync(Guid id, CancellationToken ct = default) =>
        SendAsync<PlanDto>(HttpMethod.Get, $"api/plans/{id}", null, ct);

    /// <summary>Bei 409 mit Code <c>plan-limit-confirmation</c> liefert <see cref="ApiResult{T}.ReadBody{TBody}"/> eine <see cref="PlanLimitWarning"/>.</summary>
    public Task<ApiResult<PlanDto>> CreatePlanAsync(PlanInput input, Guid? confirmDeletionOf, CancellationToken ct = default) =>
        SendAsync<PlanDto>(HttpMethod.Post, "api/plans",
            new { input.Title, input.Parameters, input.Items, ConfirmDeletionOf = confirmDeletionOf }, ct);

    public Task<ApiResult<PlanDto>> UpdatePlanAsync(Guid id, int version, PlanInput input, CancellationToken ct = default) =>
        SendAsync<PlanDto>(HttpMethod.Put, $"api/plans/{id}", new { version, input.Title, input.Parameters, input.Items }, ct);

    public Task<ApiResult<NoContent>> DeletePlanAsync(Guid id, CancellationToken ct = default) =>
        SendAsync<NoContent>(HttpMethod.Delete, $"api/plans/{id}", null, ct);

    // --- KI-Vorschläge ---------------------------------------------------------------------

    public Task<ApiResult<SuggestionDto>> RequestSuggestionAsync(TrainingParameters parameters, CancellationToken ct = default) =>
        SendAsync<SuggestionDto>(HttpMethod.Post, "api/suggestions", new { parameters }, ct);

    public Task<ApiResult<List<Finding>>> EditSuggestionAsync(Guid id, ExerciseContent draft, CancellationToken ct = default) =>
        SendAsync<List<Finding>>(HttpMethod.Put, $"api/suggestions/{id}", new { draft }, ct);

    /// <summary>Bei 422 liefert <see cref="ApiResult{T}.ReadBody{TBody}"/> ein <see cref="AdoptBlocked"/>.</summary>
    public Task<ApiResult<ExerciseDto>> AdoptSuggestionAsync(Guid id, CancellationToken ct = default) =>
        SendAsync<ExerciseDto>(HttpMethod.Post, $"api/suggestions/{id}/adopt", null, ct);

    public Task<ApiResult<NoContent>> DiscardSuggestionAsync(Guid id, CancellationToken ct = default) =>
        SendAsync<NoContent>(HttpMethod.Post, $"api/suggestions/{id}/discard", null, ct);

    // --- Kern ------------------------------------------------------------------------------

    private async Task<ApiResult<T>> SendAsync<T>(HttpMethod method, string url, object? body, CancellationToken ct)
    {
        HttpResponseMessage response;
        try
        {
            using var request = new HttpRequestMessage(method, url);
            if (body is not null) request.Content = JsonContent.Create(body, options: Json);
            response = await http.SendAsync(request, ct);
        }
        catch (HttpRequestException)
        {
            state.ReportOffline();
            return ApiResult<T>.Offline();
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            // Zeitüberschreitung des HttpClient: Der Server antwortet nicht.
            state.ReportOffline();
            return ApiResult<T>.Offline();
        }

        using (response)
        {
            var status = (int)response.StatusCode;
            if (response.IsSuccessStatusCode)
            {
                state.ReportOnline();
                if (typeof(T) == typeof(NoContent) || response.StatusCode == HttpStatusCode.NoContent)
                    return ApiResult<T>.Ok(default, status);
                return ApiResult<T>.Ok(await response.Content.ReadFromJsonAsync<T>(Json, ct), status);
            }

            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                state.ReportOnline();
                return ApiResult<T>.Unauthorized();
            }

            var json = await TryReadJsonAsync(response, ct);
            var error = TryReadApiError(json);

            // Ein 5xx ohne CoachFlow-Fehlerkörper stammt von einem Proxy oder einem Absturz: wie offline behandeln.
            if (error is null && status >= 500)
            {
                state.ReportOffline();
                return ApiResult<T>.Offline() with { HttpStatus = status, Error = new ApiError("server-unavailable", ClientMessages.ServerUnavailable) };
            }

            state.ReportOnline();
            error ??= status == 429
                ? new ApiError("too-many-requests", ClientMessages.TooManyAttempts)
                : new ApiError($"http-{status}", ClientMessages.RequestFailed);
            return ApiResult<T>.Failed(error, status, json);
        }
    }

    private static async Task<JsonElement?> TryReadJsonAsync(HttpResponseMessage response, CancellationToken ct)
    {
        try
        {
            var text = await response.Content.ReadAsStringAsync(ct);
            return string.IsNullOrWhiteSpace(text) ? null : JsonDocument.Parse(text).RootElement.Clone();
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static ApiError? TryReadApiError(JsonElement? json)
    {
        if (json is not { ValueKind: JsonValueKind.Object } obj) return null;
        if (!obj.TryGetProperty("code", out var code) || !obj.TryGetProperty("message", out var message)) return null;

        List<string>? errors = null;
        if (obj.TryGetProperty("errors", out var list) && list.ValueKind == JsonValueKind.Array)
            errors = list.EnumerateArray().Select(e => e.GetString() ?? "").ToList();
        return new ApiError(code.GetString() ?? "", message.GetString() ?? "", errors);
    }

    /// <summary>Identity meldet Fehler als Validierungsproblem mit englischen Texten; hier auf Deutsch.</summary>
    private static ApiResult<NoContent> WithIdentityMessages(ApiResult<NoContent> result)
    {
        if (result.Error is { Code: not "invalid" and not "not-found" } && result.Body is { ValueKind: JsonValueKind.Object } body
            && body.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Object)
        {
            var messages = errors.EnumerateObject().Select(e => IdentityMessage(e.Name)).Distinct().ToList();
            return result with { Error = new ApiError("registration-failed", "Die Registrierung hat nicht geklappt.", messages) };
        }
        return result;
    }

    internal static string IdentityMessage(string code) => code switch
    {
        "DuplicateUserName" or "DuplicateEmail" => "Für diese E-Mail-Adresse gibt es bereits ein Konto.",
        "InvalidEmail" or "InvalidUserName" => "Bitte gib eine gültige E-Mail-Adresse ein.",
        "PasswordTooShort" => "Das Passwort muss mindestens 10 Zeichen lang sein.",
        _ => "Die Eingaben wurden nicht akzeptiert. Bitte prüfe E-Mail und Passwort."
    };
}
