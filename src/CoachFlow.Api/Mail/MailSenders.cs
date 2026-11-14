using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;

namespace CoachFlow.Api.Mail;

public sealed record OutgoingMail(string To, string Subject, string Body);

// Der E-Mail-Dienst ist noch nicht gewählt und bleibt deshalb austauschbar.
public interface IMailSender
{
    bool IsConfigured { get; }

    Task SendAsync(OutgoingMail mail, CancellationToken ct = default);
}

public class SmtpOptions
{
    public const string Section = "Mail:Smtp";

    public string Host { get; set; } = "";

    public int Port { get; set; } = 587;

    public bool EnableSsl { get; set; } = true;

    public string Username { get; set; } = "";

    public string Password { get; set; } = "";

    public string From { get; set; } = "";
}

public sealed class SmtpMailSender(IOptions<SmtpOptions> options) : IMailSender
{
    public bool IsConfigured => true;

    public async Task SendAsync(OutgoingMail mail, CancellationToken ct = default)
    {
        var smtp = options.Value;
        using var client = new SmtpClient(smtp.Host, smtp.Port) { EnableSsl = smtp.EnableSsl };
        if (!string.IsNullOrEmpty(smtp.Username))
        {
            client.Credentials = new NetworkCredential(smtp.Username, smtp.Password);
        }

        using var message = new MailMessage(smtp.From, mail.To, mail.Subject, mail.Body);
        await client.SendMailAsync(message, ct);
    }
}

// Nur für die Entwicklung: Die E-Mail samt Link erscheint in der Konsole.
public sealed class LogMailSender(ILogger<LogMailSender> logger) : IMailSender
{
    public bool IsConfigured => true;

    public Task SendAsync(OutgoingMail mail, CancellationToken ct = default)
    {
        logger.LogInformation("E-Mail an {To}: {Subject}\n{Body}", mail.To, mail.Subject, mail.Body);
        return Task.CompletedTask;
    }
}

// Im Betrieb ohne SMTP-Einstellungen. Links dürfen dort nicht im Log landen.
public sealed class UnconfiguredMailSender : IMailSender
{
    public bool IsConfigured => false;

    public Task SendAsync(OutgoingMail mail, CancellationToken ct = default) =>
        throw new InvalidOperationException("Der E-Mail-Versand ist nicht eingerichtet.");
}
