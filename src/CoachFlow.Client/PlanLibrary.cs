using System.Text.Json;

namespace CoachFlow.Client;

/// <summary>Schlüssel-Wert-Speicher im Browser (im Web-Projekt: localStorage).</summary>
public interface IClientStore
{
    ValueTask<string?> GetAsync(string key);
    ValueTask SetAsync(string key, string value);
    ValueTask RemoveAsync(string key);
}

public sealed record PlanSnapshot(string Account, DateTimeOffset SavedAt, IReadOnlyList<PlanDto> Plans);

/// <summary>
/// Letzter Online-Stand der eigenen Pläne, damit sie offline lesbar bleiben (K15).
/// Höchstens zehn Pläne, daher genügt ein Eintrag. Beim Abmelden wird er gelöscht.
/// </summary>
public sealed class PlanCache(IClientStore store, TimeProvider time)
{
    public const string Key = "coachflow.plans.v1";

    public ValueTask SaveAsync(string account, IReadOnlyList<PlanDto> plans) =>
        store.SetAsync(Key, JsonSerializer.Serialize(new PlanSnapshot(account, time.GetUtcNow(), plans), CoachFlowApi.Json));

    public async ValueTask<PlanSnapshot?> LoadAsync()
    {
        var text = await store.GetAsync(Key);
        if (string.IsNullOrEmpty(text)) return null;
        try
        {
            return JsonSerializer.Deserialize<PlanSnapshot>(text, CoachFlowApi.Json);
        }
        catch (JsonException)
        {
            // Ein unlesbarer Stand (z. B. aus einer älteren Version) wird verworfen statt die Seite zu blockieren.
            await store.RemoveAsync(Key);
            return null;
        }
    }

    public ValueTask ClearAsync() => store.RemoveAsync(Key);
}

public abstract record PlanListResult
{
    public sealed record Online(IReadOnlyList<PlanDto> Plans) : PlanListResult;
    /// <summary>Nur lesen: lokaler Stand von <see cref="PlanSnapshot.SavedAt"/>.</summary>
    public sealed record Cached(PlanSnapshot Snapshot) : PlanListResult;
    public sealed record Unavailable(string Message) : PlanListResult;
    public sealed record Unauthorized : PlanListResult;
}

/// <summary>Lädt Pläne online und fällt ohne Verbindung auf den lokalen Stand zurück.</summary>
public sealed class PlanLibrary(CoachFlowApi api, PlanCache cache, AppState state)
{
    public const string NoLocalCopy = "Keine Verbindung, und auf diesem Gerät sind noch keine Pläne gespeichert.";

    public async Task<PlanListResult> LoadAsync(CancellationToken ct = default)
    {
        var result = await api.ListPlansAsync(ct);
        switch (result.Status)
        {
            case ApiStatus.Ok:
                var plans = result.Value ?? [];
                if (state.Account is { } account) await cache.SaveAsync(account, plans);
                return new PlanListResult.Online(plans);
            case ApiStatus.Unauthorized:
                return new PlanListResult.Unauthorized();
            case ApiStatus.Offline:
                return await FromCacheAsync();
            default:
                return new PlanListResult.Unavailable(result.Error?.Message ?? ClientMessages.RequestFailed);
        }
    }

    public async Task<(PlanDto? Plan, bool FromCache, string? Error)> GetAsync(Guid id, CancellationToken ct = default)
    {
        var result = await api.GetPlanAsync(id, ct);
        if (result.IsOk) return (result.Value, false, null);
        if (result.Status != ApiStatus.Offline) return (null, false, result.Error?.Message);

        var snapshot = await LoadSnapshotAsync();
        var plan = snapshot?.Plans.FirstOrDefault(p => p.Id == id);
        return plan is null ? (null, true, NoLocalCopy) : (plan, true, null);
    }

    /// <summary>Beim Start ohne Verbindung: Pläne aus dem Speicher anzeigen, falls vorhanden.</summary>
    public async Task<PlanListResult> FromCacheAsync()
    {
        var snapshot = await LoadSnapshotAsync();
        return snapshot is null ? new PlanListResult.Unavailable(NoLocalCopy) : new PlanListResult.Cached(snapshot);
    }

    private async Task<PlanSnapshot?> LoadSnapshotAsync()
    {
        var snapshot = await cache.LoadAsync();
        if (snapshot is not null) state.ShowingCache(snapshot.Account, snapshot.SavedAt);
        return snapshot;
    }
}
