namespace CoachFlow.Api.Exercises;

// Vorbefüllter Katalog. Feste Ids, damit die Migration die Einträge eindeutig anlegt.
public static class FocusCatalog
{
    private static readonly DateTime SeededAt = new(2026, 11, 8, 0, 0, 0, DateTimeKind.Utc);

    public static readonly IReadOnlyList<Focus> Initial =
    [
        Seed("0a6f1c2e-0001-4000-8000-000000000001", "Aufwärmen"),
        Seed("0a6f1c2e-0001-4000-8000-000000000002", "Pritschen"),
        Seed("0a6f1c2e-0001-4000-8000-000000000003", "Baggern"),
        Seed("0a6f1c2e-0001-4000-8000-000000000004", "Aufschlag"),
        Seed("0a6f1c2e-0001-4000-8000-000000000005", "Annahme"),
        Seed("0a6f1c2e-0001-4000-8000-000000000006", "Zuspiel"),
        Seed("0a6f1c2e-0001-4000-8000-000000000007", "Angriff"),
        Seed("0a6f1c2e-0001-4000-8000-000000000008", "Block"),
        Seed("0a6f1c2e-0001-4000-8000-000000000009", "Feldabwehr"),
        Seed("0a6f1c2e-0001-4000-8000-000000000010", "Spielverständnis"),
        Seed("0a6f1c2e-0001-4000-8000-000000000011", "Koordination"),
        Seed("0a6f1c2e-0001-4000-8000-000000000012", "Athletik")
    ];

    private static Focus Seed(string id, string name) => new()
    {
        Id = Guid.Parse(id),
        Sport = Sports.Volleyball,
        Name = name,
        NormalizedName = Text.Normalize(name),
        CreatedAt = SeededAt
    };
}
