namespace CoachFlow.Domain;

/// <summary>Plansicht auf eine Übung: Personenangaben und geplante Dauer.</summary>
public sealed record PlanEntry(
    string Title,
    int PlannedMinutes,
    int? MinParticipants,
    int? MaxParticipants,
    IReadOnlyList<ExerciseOption> Options);

/// <summary>Regeln für Trainingspläne. Alle Befunde sind Hinweise: Entwürfe bleiben immer speicherbar.</summary>
public static class PlanRules
{
    public static int TotalMinutes(IEnumerable<PlanEntry> entries) => entries.Sum(e => e.PlannedMinutes);

    public static int RemainingMinutes(TrainingParameters parameters, IEnumerable<PlanEntry> entries) =>
        parameters.DurationMinutes - TotalMinutes(entries);

    public static IReadOnlyList<Finding> Check(TrainingParameters parameters, IReadOnlyList<PlanEntry> entries)
    {
        var findings = new List<Finding>();

        if (parameters.ParticipantCount is < TrainingParameters.TypicalMinParticipants or > TrainingParameters.TypicalMaxParticipants)
            findings.Add(Warning(FindingCodes.ParticipantCountUntypical,
                $"{parameters.ParticipantCount} Personen liegen außerhalb des typischen Bereichs von 5 bis 14."));

        var total = TotalMinutes(entries);
        if (total > parameters.DurationMinutes)
            findings.Add(Warning(FindingCodes.PlanDurationExceeded,
                $"Der Plan umfasst {total} Minuten, das Training dauert {parameters.DurationMinutes} Minuten."));

        foreach (var entry in entries)
            CheckEntry(entry, parameters, findings);
        return findings;
    }

    private static void CheckEntry(PlanEntry entry, TrainingParameters parameters, List<Finding> findings)
    {
        if (entry.MinParticipants is { } min && min > parameters.ParticipantCount)
            findings.Add(Warning(FindingCodes.TooFewParticipants,
                $"„{entry.Title}“ benötigt {min} Personen, verfügbar sind {parameters.ParticipantCount}."));

        if (entry.MaxParticipants is { } max && max < parameters.ParticipantCount)
            findings.Add(Warning(FindingCodes.TooManyParticipants,
                $"„{entry.Title}“ ist für höchstens {max} Personen ausgelegt, geplant sind {parameters.ParticipantCount}."));

        if (parameters.Spread == ExperienceSpread.Anpassungen && !HasOption(entry, OptionKind.Anpassung))
            findings.Add(Warning(FindingCodes.AdaptationMissing, $"„{entry.Title}“ hat bei Streuung 2 keine Anpassung."));

        if (parameters.Spread == ExperienceSpread.Varianten && !HasOption(entry, OptionKind.Variante))
            findings.Add(Warning(FindingCodes.VariantMissing, $"„{entry.Title}“ hat bei Streuung 3 keine Variante."));
    }

    private static bool HasOption(PlanEntry entry, OptionKind kind) =>
        entry.Options.Any(o => o.Kind == kind && o.IsComplete);

    private static Finding Warning(string code, string message) => new(code, FindingSeverity.Warning, message);
}
