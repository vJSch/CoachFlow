namespace CoachFlow.Domain;

public enum ExperienceLevel
{
    JugendKleinfeld,      // U12, U13, U14
    JugendGrossfeld,      // U15 bis U18
    ErwachseneAnfaenger,
    Hobby,
    KreisligaBisLandesliga,
    VerbandsligaBisOberliga
}

/// <summary>Stufe 1: keine Anpassung, 2: Anpassungen, 3: Varianten.</summary>
public enum ExperienceSpread
{
    Keine = 1,
    Anpassungen = 2,
    Varianten = 3
}

public enum OptionKind
{
    Anpassung,
    Variante
}

public enum FindingSeverity
{
    /// <summary>Verhindert die Übernahme eines KI-Vorschlags. Pläne bleiben trotzdem speicherbar.</summary>
    Blocker,
    Warning
}
