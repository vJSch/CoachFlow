namespace CoachFlow.Domain;

/// <summary>Strukturregeln für jede gespeicherte Übung, unabhängig von einem konkreten Training.</summary>
public static class ExerciseRules
{
    public static IReadOnlyList<Finding> CheckStructure(ExerciseContent content)
    {
        var findings = new List<Finding>();

        if (string.IsNullOrWhiteSpace(content.Title))
            findings.Add(Blocker(FindingCodes.TitleMissing, "Der Titel fehlt."));
        if (content.DurationMinutes is null or < 1)
            findings.Add(Blocker(FindingCodes.DurationMissing, "Die Dauer fehlt oder ist ungültig."));
        if (content.MinParticipants is null or < 1)
            findings.Add(Blocker(FindingCodes.MinParticipantsMissing, "Die benötigte Personenzahl fehlt oder ist ungültig."));
        if (content.MinParticipants is { } min && content.MaxParticipants is { } max && max < min)
            findings.Add(Blocker(FindingCodes.ParticipantRangeInvalid,
                $"Die Höchstzahl ({max}) liegt unter der Mindestzahl ({min})."));
        if (content.FocusAreaIds.Distinct().Count() > TrainingParameters.MaxFocusAreas)
            findings.Add(Blocker(FindingCodes.TooManyFocusAreas, "Eine Übung hat höchstens zwei Trainingsschwerpunkte."));

        foreach (var option in content.Options.Where(o => !o.IsComplete))
            findings.Add(Blocker(FindingCodes.OptionIncomplete,
                $"Eine {option.Kind} ist unvollständig: Titel und Beschreibung oder ein Verweis auf eine Übung sind nötig."));

        return findings;
    }

    private static Finding Blocker(string code, string message) => new(code, FindingSeverity.Blocker, message);
}
