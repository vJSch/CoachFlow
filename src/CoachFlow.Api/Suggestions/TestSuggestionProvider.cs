using System.Text.Json;
using Microsoft.Extensions.Options;

namespace CoachFlow.Api.Suggestions;

// Vorbereitete Antworten für Entwicklung und erste Tests. Ruft keinen externen Dienst auf und kostet nichts.
public sealed class TestSuggestionProvider(IOptions<AiOptions> options) : IExerciseSuggestionProvider
{
    public async Task<ProviderResult> SuggestAsync(ProviderInput input, CancellationToken ct)
    {
        await Task.Delay(options.Value.Test.Delay, ct);

        var focus = input.FocusNames.FirstOrDefault() ?? "Ballkontrolle";
        var duration = Math.Clamp(input.DurationMinutes / 4, 5, 30);
        var templates = new[]
        {
            (Name: $"{focus} in Zweiergruppen",
                Description: $"Je zwei Spielende üben {focus} im Wechsel. Nach jeweils 10 Wiederholungen tauschen die Rollen."),
            (Name: $"{focus}-Stationen",
                Description: $"Drei Stationen mit steigender Schwierigkeit zum Thema {focus}. Wechsel alle 4 Minuten."),
            (Name: $"Kleines Spiel mit Schwerpunkt {focus}",
                Description: $"Spiel 3 gegen 3 auf verkleinertem Feld. Punkte zählen doppelt, wenn sie über {focus} entstehen.")
        };

        var suggestions = templates.Take(input.SuggestionCount).Select(t => new
        {
            name = t.Name,
            // Kennzeichnung, damit niemand die Beispiele für echte KI-Vorschläge hält.
            description = $"[Testvorschlag] {t.Description}",
            durationMinutes = duration,
            minParticipants = (int?)null,
            maxParticipants = (int?)null
        });

        return new ProviderResult(JsonSerializer.Serialize(new { suggestions }), 0, 0, 0m);
    }
}
