using System.Text.Json;

namespace CoachFlow.Client;

public enum ApiStatus
{
    Ok,
    /// <summary>Die API hat geantwortet und abgelehnt; <see cref="ApiResult{T}.Error"/> enthält den Grund.</summary>
    Failed,
    /// <summary>Nicht (mehr) angemeldet.</summary>
    Unauthorized,
    /// <summary>Kein Netz oder Server nicht erreichbar.</summary>
    Offline
}

public sealed record ApiResult<T>(ApiStatus Status, T? Value, ApiError? Error, int HttpStatus, JsonElement? Body)
{
    public bool IsOk => Status == ApiStatus.Ok;

    public static ApiResult<T> Ok(T? value, int httpStatus) => new(ApiStatus.Ok, value, null, httpStatus, null);
    public static ApiResult<T> Failed(ApiError error, int httpStatus, JsonElement? body) => new(ApiStatus.Failed, default, error, httpStatus, body);
    public static ApiResult<T> Unauthorized() => new(ApiStatus.Unauthorized, default, new ApiError("unauthorized", ClientMessages.SignedOut), 401, null);
    public static ApiResult<T> Offline() => new(ApiStatus.Offline, default, new ApiError("offline", ClientMessages.Offline), 0, null);

    /// <summary>Liest einen besonderen Fehlerkörper, z. B. die Plan-Limit-Warnung.</summary>
    public TBody? ReadBody<TBody>() => Body is { } body ? body.Deserialize<TBody>(CoachFlowApi.Json) : default;
}

/// <summary>Leerer Rückgabewert für Aufrufe ohne Antwortkörper.</summary>
public readonly record struct NoContent;

public static class ClientMessages
{
    public const string Offline = "Keine Verbindung zum Server. Bitte versuche es später erneut.";
    public const string SignedOut = "Bitte melde dich an.";
    public const string ServerUnavailable = "Der Server ist gerade nicht erreichbar. Bitte versuche es später erneut.";
    public const string RequestFailed = "Die Anfrage ist fehlgeschlagen. Bitte versuche es erneut.";
    public const string TooManyAttempts = "Zu viele Versuche. Bitte warte eine Minute.";
    public const string LoginFailed = "E-Mail oder Passwort ist falsch, oder das Konto ist nach mehreren Fehlversuchen für 15 Minuten gesperrt.";

    // Vorgegebene Texte (K12), siehe docs/entscheidungen-stufe1.md.
    public const string SuggestionSlow = "Die Erstellung dauert länger. Du kannst weiter warten oder abbrechen.";
    public const string SuggestionTimedOut = "Es konnte rechtzeitig kein Übungsvorschlag erstellt werden. Deine Eingaben bleiben erhalten.";
    public const string SuggestionOffline = "Übungsvorschläge sind nur mit Verbindung möglich. Deine Eingaben bleiben erhalten.";
}
