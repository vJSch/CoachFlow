namespace CoachFlow.Client;

/// <summary>
/// Sitzungs- und Verbindungszustand für die Oberfläche. Offline ist nur Lesen erlaubt (Entscheidung K15).
/// </summary>
public sealed class AppState
{
    public string? Account { get; private set; }
    public bool IsOffline { get; private set; }

    /// <summary>Zeitpunkt des angezeigten lokalen Stands, solange offline gelesen wird.</summary>
    public DateTimeOffset? CachedAt { get; private set; }

    public bool CanEdit => !IsOffline && Account is not null;

    public event Action? Changed;

    public void SignedIn(string account)
    {
        Account = account;
        Notify();
    }

    public void SignedOut()
    {
        Account = null;
        CachedAt = null;
        Notify();
    }

    public void ReportOffline()
    {
        if (IsOffline) return;
        IsOffline = true;
        Notify();
    }

    public void ReportOnline()
    {
        if (!IsOffline && CachedAt is null) return;
        IsOffline = false;
        CachedAt = null;
        Notify();
    }

    /// <summary>Offline mit lokal gespeichertem Stand; das Konto stammt aus dem Speicher.</summary>
    public void ShowingCache(string account, DateTimeOffset savedAt)
    {
        Account ??= account;
        IsOffline = true;
        CachedAt = savedAt;
        Notify();
    }

    private void Notify() => Changed?.Invoke();
}
