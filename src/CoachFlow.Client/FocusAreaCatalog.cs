using System.Text.Json;

namespace CoachFlow.Client;

/// <summary>
/// Schwerpunkte mit lokalem Stand. Offline braucht die Planansicht die Namen, sonst sähe man nur Ids.
/// </summary>
public sealed class FocusAreaCatalog(CoachFlowApi api, IClientStore store)
{
    public const string Key = "coachflow.focus-areas.v1";

    public async Task<IReadOnlyList<FocusAreaDto>> LoadAsync(CancellationToken ct = default)
    {
        var result = await api.ListFocusAreasAsync(ct);
        if (result is { IsOk: true, Value: { } areas })
        {
            await store.SetAsync(Key, JsonSerializer.Serialize(areas, CoachFlowApi.Json));
            return areas;
        }

        var text = await store.GetAsync(Key);
        if (string.IsNullOrEmpty(text)) return [];
        try
        {
            return JsonSerializer.Deserialize<List<FocusAreaDto>>(text, CoachFlowApi.Json) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    public static string Name(IReadOnlyList<FocusAreaDto> areas, Guid id) =>
        areas.FirstOrDefault(a => a.Id == id)?.Name ?? "Unbekannter Schwerpunkt";

    public ValueTask ClearAsync() => store.RemoveAsync(Key);
}
