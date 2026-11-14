namespace CoachFlow.Api.Accounts;

public class Invitation
{
    public Guid Id { get; set; }

    // Kleingeschrieben und ohne Leerzeichen, damit Vergleiche eindeutig sind.
    public string Email { get; set; } = "";

    // Gespeichert wird nur der Hash. Der Token selbst steht ausschließlich im Link der E-Mail.
    public string TokenHash { get; set; } = "";

    public DateTime ExpiresAt { get; set; }

    public DateTime? RedeemedAt { get; set; }

    public string? InvitedById { get; set; }

    public AppUser? InvitedBy { get; set; }
}
