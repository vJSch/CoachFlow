namespace CoachFlow.Domain;

/// <summary>
/// Prüft einen KI-Vorschlag vor der Übernahme. Erkannt werden nur maschinell prüfbare Mängel;
/// die fachliche Eignung beurteilt die Trainerin oder der Trainer.
/// </summary>
public static class SuggestionValidator
{
    public static IReadOnlyList<Finding> Validate(ExerciseContent draft, TrainingParameters parameters)
    {
        var findings = new List<Finding>(ExerciseRules.CheckStructure(draft));

        // Bei eigenen Übungen ist die Beschreibung freiwillig, ein KI-Vorschlag ohne sie ist unbrauchbar.
        if (string.IsNullOrWhiteSpace(draft.Description))
            findings.Add(Blocker(FindingCodes.DescriptionMissing, "Die Beschreibung fehlt."));

        CheckParticipants(draft, parameters, findings);
        CheckDuration(draft, parameters, findings);
        CheckSpread(draft, parameters, findings);
        CheckFit(draft, parameters, findings);
        return findings;
    }

    public static bool HasBlockers(IEnumerable<Finding> findings) =>
        findings.Any(f => f.Severity == FindingSeverity.Blocker);

    private static void CheckParticipants(ExerciseContent draft, TrainingParameters parameters, List<Finding> findings)
    {
        if (draft.MinParticipants is { } required && required > parameters.ParticipantCount)
            findings.Add(Blocker(FindingCodes.TooFewParticipants,
                $"Die Übung benötigt {required} Personen, verfügbar sind {parameters.ParticipantCount}."));

        if (draft.MaxParticipants is { } limit && limit < parameters.ParticipantCount)
            findings.Add(Warning(FindingCodes.TooManyParticipants,
                $"Die Übung ist für höchstens {limit} Personen ausgelegt, geplant sind {parameters.ParticipantCount}."));
    }

    private static void CheckDuration(ExerciseContent draft, TrainingParameters parameters, List<Finding> findings)
    {
        if (draft.DurationMinutes is { } duration && duration > parameters.DurationMinutes)
            findings.Add(Blocker(FindingCodes.DurationExceedsSession,
                $"Die Übung dauert {duration} Minuten, das Training nur {parameters.DurationMinutes} Minuten."));
    }

    private static void CheckSpread(ExerciseContent draft, TrainingParameters parameters, List<Finding> findings)
    {
        OptionKind? required = parameters.Spread switch
        {
            ExperienceSpread.Anpassungen => OptionKind.Anpassung,
            ExperienceSpread.Varianten => OptionKind.Variante,
            _ => null
        };
        if (required is null || draft.Options.Any(o => o.Kind == required && o.IsComplete)) return;

        findings.Add(required == OptionKind.Variante
            ? Blocker(FindingCodes.VariantMissing, "Bei Streuung 3 ist mindestens eine Variante nötig.")
            : Blocker(FindingCodes.AdaptationMissing, "Bei Streuung 2 ist mindestens eine Anpassung nötig."));
    }

    private static void CheckFit(ExerciseContent draft, TrainingParameters parameters, List<Finding> findings)
    {
        if (draft.Levels.Count > 0 && !draft.Levels.Contains(parameters.Level))
            findings.Add(Warning(FindingCodes.LevelMismatch, "Der Vorschlag ist nicht für das angegebene Niveau ausgewiesen."));

        if (parameters.FocusAreaIds.Count > 0 && !draft.FocusAreaIds.Intersect(parameters.FocusAreaIds).Any())
            findings.Add(Warning(FindingCodes.FocusMismatch, "Der Vorschlag passt zu keinem der gewählten Schwerpunkte."));
    }

    private static Finding Blocker(string code, string message) => new(code, FindingSeverity.Blocker, message);
    private static Finding Warning(string code, string message) => new(code, FindingSeverity.Warning, message);
}
