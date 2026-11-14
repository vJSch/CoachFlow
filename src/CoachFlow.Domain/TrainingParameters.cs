namespace CoachFlow.Domain;

/// <summary>Rahmendaten einer Trainingseinheit. Enthält bewusst nur Anzahlen, keine Spielernamen.</summary>
public sealed record TrainingParameters(
    int ParticipantCount,
    ExperienceLevel Level,
    ExperienceSpread Spread,
    int DurationMinutes,
    IReadOnlyList<Guid> FocusAreaIds)
{
    public const int MaxFocusAreas = 2;
    public const int TypicalMinParticipants = 5;
    public const int TypicalMaxParticipants = 14;

    /// <summary>Formale Fehler. Der Bereich 5 bis 14 ist nur typisch und daher kein Fehler.</summary>
    public IReadOnlyList<string> GetErrors(bool forAiRequest)
    {
        var errors = new List<string>();
        if (ParticipantCount < 1) errors.Add("Die Teilnehmerzahl muss mindestens 1 sein.");
        if (DurationMinutes < 1) errors.Add("Die Trainingsdauer muss mindestens 1 Minute betragen.");

        var focusCount = FocusAreaIds.Distinct().Count();
        if (focusCount > MaxFocusAreas) errors.Add("Es sind höchstens zwei Trainingsschwerpunkte möglich.");
        if (forAiRequest && focusCount == 0) errors.Add("Für einen KI-Vorschlag ist mindestens ein Trainingsschwerpunkt nötig.");
        return errors;
    }
}
