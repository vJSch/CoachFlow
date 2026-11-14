namespace CoachFlow.Api.Accounts;

public class AccountOptions
{
    public const string Section = "Accounts";

    // Adresse, unter der Nutzende die App aufrufen. Aus ihr entstehen die Links in E-Mails.
    public string PublicBaseUrl { get; set; } = "";

    public TimeSpan InvitationLifetime { get; set; } = TimeSpan.FromDays(3);

    public TimeSpan PasswordResetLifetime { get; set; } = TimeSpan.FromHours(2);

    public string Link(string pathAndFragment) => PublicBaseUrl.TrimEnd('/') + pathAndFragment;
}
