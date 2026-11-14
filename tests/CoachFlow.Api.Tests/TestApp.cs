using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using CoachFlow.Api.Accounts;
using CoachFlow.Api.Mail;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CoachFlow.Api.Tests;

public sealed class ManualTimeProvider : TimeProvider
{
    private DateTimeOffset _now = new(2026, 11, 1, 12, 0, 0, TimeSpan.Zero);

    public override DateTimeOffset GetUtcNow() => _now;

    public void Advance(TimeSpan by) => _now += by;
}

public sealed class CapturingMailSender : IMailSender
{
    public List<OutgoingMail> Sent { get; } = [];

    public bool IsConfigured => true;

    public Task SendAsync(OutgoingMail mail, CancellationToken ct = default)
    {
        Sent.Add(mail);
        return Task.CompletedTask;
    }
}

// Eine App mit eigener Datenbank, abgefangenen E-Mails und steuerbarer Uhr.
public sealed partial class TestApp : IAsyncDisposable
{
    public const string Password = "richtig-langes-passwort";

    private readonly TemporaryDatabase _database = new();

    public TestApp(
        bool mailConfigured = true,
        IReadOnlyDictionary<string, string>? settings = null,
        Action<IServiceCollection>? configureServices = null)
    {
        Factory = new CoachFlowFactory(_database.ConnectionString, migrate: true, services =>
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(Time);
            if (mailConfigured)
            {
                services.RemoveAll<IMailSender>();
                services.AddSingleton<IMailSender>(Mail);
            }

            configureServices?.Invoke(services);
        }, settings);
    }

    public CoachFlowFactory Factory { get; }

    public ManualTimeProvider Time { get; } = new();

    public CapturingMailSender Mail { get; } = new();

    public HttpClient CreateClient() => Factory.CreateClient();

    // Wie beim Einrichten: Einladung über die Kommandozeile, Token aus der Ausgabe.
    public async Task<string> InviteViaCommandAsync(string email)
    {
        var output = new StringWriter();
        var exitCode = await AccountCommands.InviteAsync(Factory.Services, email, output);
        Assert.Equal(0, exitCode);
        return InvitationToken(output.ToString());
    }

    public async Task<HttpClient> CreateUserAsync(string email, string name = "Testperson")
    {
        var token = await InviteViaCommandAsync(email);
        var client = CreateClient();
        var response = await client.PostAsJsonAsync("/api/account/register", new { token, name, password = Password });
        response.EnsureSuccessStatusCode();
        return client;
    }

    public static string InvitationToken(string text) => InvitationTokenPattern().Match(text).Groups[1].Value;

    public static (string Email, string Token) ResetLink(string text)
    {
        var match = ResetLinkPattern().Match(text);
        return (Uri.UnescapeDataString(match.Groups[1].Value), Uri.UnescapeDataString(match.Groups[2].Value));
    }

    public static async Task<string?> DetailAsync(HttpResponseMessage response)
    {
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.TryGetProperty("detail", out var detail) ? detail.GetString() : null;
    }

    public async ValueTask DisposeAsync()
    {
        await Factory.DisposeAsync();
        _database.Dispose();
    }

    [GeneratedRegex(@"/einladung#token=([A-Za-z0-9_-]+)")]
    private static partial Regex InvitationTokenPattern();

    [GeneratedRegex(@"/passwort-neu#email=([^&\s]+)&token=(\S+)")]
    private static partial Regex ResetLinkPattern();
}
