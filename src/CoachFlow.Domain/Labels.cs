namespace CoachFlow.Domain;

/// <summary>
/// Deutsche Bezeichnungen der Fachbegriffe. Liegt in der Domäne, weil Oberfläche und Export dieselben Texte zeigen.
/// </summary>
public static class Labels
{
    public static readonly IReadOnlyList<ExperienceLevel> AllLevels = Enum.GetValues<ExperienceLevel>();
    public static readonly IReadOnlyList<ExperienceSpread> AllSpreads = Enum.GetValues<ExperienceSpread>();

    public static string Level(ExperienceLevel level) => level switch
    {
        ExperienceLevel.JugendKleinfeld => "Jugend Kleinfeld (U12–U14)",
        ExperienceLevel.JugendGrossfeld => "Jugend Großfeld (U15–U18)",
        ExperienceLevel.ErwachseneAnfaenger => "Erwachsene Anfänger",
        ExperienceLevel.Hobby => "Hobby",
        ExperienceLevel.KreisligaBisLandesliga => "Kreisliga bis Landesliga",
        ExperienceLevel.VerbandsligaBisOberliga => "Verbandsliga bis Oberliga",
        _ => level.ToString()
    };

    public static string Spread(ExperienceSpread spread) => spread switch
    {
        ExperienceSpread.Keine => "1 – ähnliche Erfahrung, keine Anpassung",
        ExperienceSpread.Anpassungen => "2 – gemischt, Anpassungen nötig",
        ExperienceSpread.Varianten => "3 – stark gemischt, Varianten nötig",
        _ => spread.ToString()
    };

    public static string Option(OptionKind kind) => kind == OptionKind.Anpassung ? "Anpassung" : "Variante";

    public static string Severity(FindingSeverity severity) =>
        severity == FindingSeverity.Blocker ? "Muss korrigiert werden" : "Hinweis";

    public static string Participants(int? min, int? max) => (min, max) switch
    {
        (null, null) => "–",
        ({ } a, null) => $"ab {a}",
        (null, { } b) => $"bis {b}",
        ({ } a, { } b) when a == b => $"{a}",
        ({ } a, { } b) => $"{a}–{b}"
    };
}
