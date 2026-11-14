using CoachFlow.Api.Exercises;
using CoachFlow.Api.Plans;

namespace CoachFlow.Api.Suggestions;

// Text für einen echten Anbieter (Schritt 7). Er entsteht nur aus ProviderInput,
// persönliche Daten können deshalb nicht hineingeraten.
public static class SuggestionPrompt
{
    public static string Build(ProviderInput input)
    {
        var focuses = input.FocusNames.Count == 0 ? "keine Vorgabe" : string.Join(", ", input.FocusNames);
        return $$"""
            Schlage {{input.SuggestionCount}} Übungen für ein Volleyballtraining vor.

            Gruppe: {{input.ParticipantCount}} Teilnehmende, Niveau {{LevelText(input.Level)}}, Leistungsstreuung {{SpreadText(input.Spread)}}.
            Trainingsdauer insgesamt: {{input.DurationMinutes}} Minuten.
            Schwerpunkte: {{focuses}}.

            Antworte ausschließlich mit JSON in dieser Form:
            {"suggestions":[{"name":"…","description":"…","durationMinutes":10,"minParticipants":null,"maxParticipants":null}]}
            name höchstens {{ExerciseEndpoints.MaxNameLength}} Zeichen, description höchstens 1000 Zeichen auf Deutsch,
            durationMinutes zwischen 1 und {{ExerciseEndpoints.MaxDurationMinutes}}.
            """;
    }

    private static string LevelText(Level level) => level switch
    {
        Level.MiniVolleyball => "Mini-Volleyball",
        Level.MidiVolleyball => "Midi-Volleyball",
        Level.Beginner => "Anfänger",
        Level.Advanced => "Fortgeschrittene",
        _ => level.ToString()
    };

    private static string SpreadText(Spread spread) => spread switch
    {
        Spread.Low => "gering",
        Spread.Medium => "mittel",
        Spread.High => "hoch",
        _ => spread.ToString()
    };
}
