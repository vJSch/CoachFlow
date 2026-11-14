using System.Text.Json;
using CoachFlow.Api.Exercises;

namespace CoachFlow.Api.Suggestions;

// Prüft nur das Format. Ob eine Übung fachlich taugt, entscheiden die Nutzenden.
public static class SuggestionParser
{
    // Unbrauchbare Einträge fallen weg. Leer heißt: Die Antwort als Ganzes ist unbrauchbar.
    public static IReadOnlyList<ExerciseSuggestion> Parse(string json, int maxCount)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json);
        }
        catch (JsonException)
        {
            return [];
        }

        using (document)
        {
            if (document.RootElement.ValueKind != JsonValueKind.Object
                || !document.RootElement.TryGetProperty("suggestions", out var items)
                || items.ValueKind != JsonValueKind.Array)
            {
                return [];
            }

            return items.EnumerateArray()
                .Select(TryRead)
                .OfType<ExerciseSuggestion>()
                .Take(maxCount)
                .ToList();
        }
    }

    private static ExerciseSuggestion? TryRead(JsonElement item)
    {
        if (item.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var name = Text.Clean(String(item, "name"));
        var description = String(item, "description")?.Trim() ?? "";
        var duration = Int(item, "durationMinutes");
        var min = Int(item, "minParticipants");
        var max = Int(item, "maxParticipants");

        var valid = name.Length is > 0 and <= ExerciseEndpoints.MaxNameLength
            && description.Length <= ExerciseEndpoints.MaxDescriptionLength
            && duration is >= 1 and <= ExerciseEndpoints.MaxDurationMinutes
            && min is null or (>= 1 and <= ExerciseEndpoints.MaxParticipants)
            && max is null or (>= 1 and <= ExerciseEndpoints.MaxParticipants)
            && !(min > max);

        return valid ? new ExerciseSuggestion(name, description, duration!.Value, min, max) : null;
    }

    private static string? String(JsonElement item, string property) =>
        item.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static int? Int(JsonElement item, string property) =>
        item.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number)
            ? number
            : null;
}
