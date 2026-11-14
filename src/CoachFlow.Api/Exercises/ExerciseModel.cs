using System.Globalization;
using CoachFlow.Api.Accounts;

namespace CoachFlow.Api.Exercises;

public static class Sports
{
    // Zunächst nur Volleyball. Das Feld hält die Tür für weitere Sportarten offen.
    public const string Volleyball = "volleyball";
}

public enum Level
{
    MiniVolleyball,
    MidiVolleyball,
    Beginner,
    Advanced
}

public enum ExerciseOrigin
{
    Own,
    Ai
}

public class Focus
{
    public Guid Id { get; set; }

    public string Sport { get; set; } = Sports.Volleyball;

    public string Name { get; set; } = "";

    // Eindeutig je Sportart, damit „Annahme“ und „ annahme “ nicht doppelt entstehen.
    public string NormalizedName { get; set; } = "";

    public string? CreatedById { get; set; }

    public AppUser? CreatedBy { get; set; }

    public DateTime CreatedAt { get; set; }
}

public class Exercise
{
    public Guid Id { get; set; }

    public string Sport { get; set; } = Sports.Volleyball;

    public string Name { get; set; } = "";

    public string Description { get; set; } = "";

    public int DefaultDurationMinutes { get; set; }

    public int? MinParticipants { get; set; }

    public int? MaxParticipants { get; set; }

    // Leer bedeutet: für alle Niveaus geeignet.
    public Level? Level { get; set; }

    public ExerciseOrigin Origin { get; set; }

    public List<Focus> Focuses { get; set; } = [];

    // Kleingeschriebener Name und Beschreibung. SQLite vergleicht Umlaute sonst nur mit exakter Schreibweise.
    public string SearchText { get; set; } = "";

    // Schutz gegen unbemerktes Überschreiben bei gemeinsamer Bearbeitung.
    public int Version { get; set; }

    public string? UpdatedById { get; set; }

    public AppUser? UpdatedBy { get; set; }

    public DateTime UpdatedAt { get; set; }
}

public static class Text
{
    // Sortierung nach deutschen Regeln, Umlaute also bei ihren Grundbuchstaben.
    public static readonly StringComparer GermanOrder =
        StringComparer.Create(CultureInfo.GetCultureInfo("de-DE"), ignoreCase: true);

    // Entfernt Leerzeichen am Rand und fasst innere Leerzeichen zusammen.
    public static string Clean(string? value) =>
        string.Join(' ', (value ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    public static string Normalize(string? value) => Clean(value).ToLowerInvariant();
}
