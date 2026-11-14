using CoachFlow.Infrastructure;
using Microsoft.AspNetCore.Identity;

namespace CoachFlow.Api;

/// <summary>
/// Platzhalter, bis ein E-Mail-Dienst gewählt ist. Links zum Zurücksetzen des Passworts sind geheim
/// und erscheinen deshalb nur in der Entwicklungsumgebung im Log, nie im Betrieb.
/// </summary>
internal sealed partial class LoggingEmailSender(ILogger<LoggingEmailSender> logger, IHostEnvironment environment)
    : IEmailSender<CoachFlowUser>
{
    public Task SendConfirmationLinkAsync(CoachFlowUser user, string email, string confirmationLink) =>
        Write(email, "Bestätigungslink", confirmationLink);

    public Task SendPasswordResetLinkAsync(CoachFlowUser user, string email, string resetLink) =>
        Write(email, "Link zum Zurücksetzen", resetLink);

    public Task SendPasswordResetCodeAsync(CoachFlowUser user, string email, string resetCode) =>
        Write(email, "Code zum Zurücksetzen", resetCode);

    private Task Write(string email, string kind, string secret)
    {
        if (environment.IsDevelopment()) LogDevelopment(email, kind, secret);
        else LogMissingService(kind);
        return Task.CompletedTask;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "E-Mail an {Email} (nur Entwicklung): {Kind}: {Secret}")]
    private partial void LogDevelopment(string email, string kind, string secret);

    [LoggerMessage(Level = LogLevel.Warning, Message = "{Kind} konnte nicht versendet werden: Es ist kein E-Mail-Dienst eingerichtet.")]
    private partial void LogMissingService(string kind);
}
