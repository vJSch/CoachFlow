using CoachFlow.Api.Mail;

namespace CoachFlow.Api.Accounts;

public static class AccountMails
{
    public static OutgoingMail Invitation(string to, string invitedByName, string link, TimeSpan lifetime) => new(
        to,
        "Einladung zu CoachFlow",
        $"""
        Hallo,

        {invitedByName} lädt dich zu CoachFlow ein, der Trainingsplanung für Volleyball.

        Über diesen Link legst du dein Konto an:
        {link}

        Der Link gilt {FormatLifetime(lifetime)} und lässt sich nur einmal verwenden.
        Falls du mit dieser Einladung nichts anfangen kannst, ignoriere diese E-Mail.
        """);

    public static OutgoingMail PasswordReset(string to, string link, TimeSpan lifetime) => new(
        to,
        "Neues Passwort für CoachFlow",
        $"""
        Hallo,

        für dein CoachFlow-Konto wurde ein neues Passwort angefordert.

        Über diesen Link legst du es fest:
        {link}

        Der Link gilt {FormatLifetime(lifetime)} und lässt sich nur einmal verwenden.
        Falls du kein neues Passwort angefordert hast, ignoriere diese E-Mail. Dein bisheriges Passwort bleibt dann gültig.
        """);

    public static string FormatLifetime(TimeSpan lifetime) => lifetime.TotalHours >= 24
        ? $"{lifetime.TotalDays:0.#} Tage"
        : $"{lifetime.TotalHours:0.#} Stunden";
}
