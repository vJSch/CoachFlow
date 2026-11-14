using CoachFlow.Domain;

namespace CoachFlow.Client;

/// <summary>Zeitgrenzen im Client (A1): Hinweis ab 5 s, harter Abbruch bei 15 s.</summary>
public sealed record SuggestionTiming(TimeSpan HintAfter, TimeSpan HardLimit)
{
    public static readonly SuggestionTiming Default = new(TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(15));
}

public abstract record SuggestionAttempt
{
    public sealed record Created(SuggestionDto Suggestion) : SuggestionAttempt;
    /// <summary>Abgelehnt oder fehlgeschlagen; <see cref="Message"/> ist der Text für die Oberfläche.</summary>
    public sealed record Failed(string Code, string Message) : SuggestionAttempt;
    public sealed record TimedOut(string Message) : SuggestionAttempt;
    public sealed record Cancelled : SuggestionAttempt;
    public sealed record Unauthorized : SuggestionAttempt;
}

/// <summary>
/// Fordert einen Vorschlag an und garantiert, dass die Oberfläche spätestens nach <see cref="SuggestionTiming.HardLimit"/>
/// eine Antwort hat, auch wenn der Server nie antwortet. Eingaben werden hier nicht verändert.
/// </summary>
public sealed class SuggestionRequester(CoachFlowApi api, TimeProvider time, SuggestionTiming timing)
{
    public async Task<SuggestionAttempt> RequestAsync(TrainingParameters parameters, Action onSlow, CancellationToken userCancel)
    {
        using var deadline = new CancellationTokenSource(timing.HardLimit, time);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(deadline.Token, userCancel);
        using var hint = time.CreateTimer(_ => onSlow(), null, timing.HintAfter, Timeout.InfiniteTimeSpan);

        try
        {
            // WaitAsync beendet das Warten auch dann, wenn die Anfrage den Abbruch ignoriert.
            var result = await api.RequestSuggestionAsync(parameters, linked.Token).WaitAsync(linked.Token);
            return result.Status switch
            {
                ApiStatus.Ok when result.Value is { } suggestion => new SuggestionAttempt.Created(suggestion),
                ApiStatus.Unauthorized => new SuggestionAttempt.Unauthorized(),
                ApiStatus.Offline => new SuggestionAttempt.Failed("offline", ClientMessages.SuggestionOffline),
                // 504 meldet der Server nach seiner eigenen Frist; der Text ist derselbe wie beim Client-Abbruch.
                _ when result.HttpStatus == 504 => new SuggestionAttempt.TimedOut(ClientMessages.SuggestionTimedOut),
                _ => new SuggestionAttempt.Failed(result.Error?.Code ?? "unknown", result.Error?.Message ?? ClientMessages.RequestFailed)
            };
        }
        catch (OperationCanceledException) when (userCancel.IsCancellationRequested)
        {
            return new SuggestionAttempt.Cancelled();
        }
        catch (OperationCanceledException) when (deadline.IsCancellationRequested)
        {
            return new SuggestionAttempt.TimedOut(ClientMessages.SuggestionTimedOut);
        }
    }
}
