using CoachFlow.Application.Exercises;

namespace CoachFlow.Application.Plans;

/// <summary>Port für den Export eines Plans (K7: xlsx). Die Bibliothek dafür steckt im Adapter.</summary>
public interface IPlanExporter
{
    string ContentType { get; }
    string FileExtension { get; }

    /// <param name="focusAreas">Zum Auflösen der Schwerpunkt-Ids in Namen.</param>
    void Export(TrainingPlan plan, IReadOnlyList<FocusArea> focusAreas, Stream output);
}
