using CoachFlow.Api.Data;
using Microsoft.Extensions.Options;

namespace CoachFlow.Api.Accounts;

// Kommandozeile für Betreibende, vor allem für das erste Konto: dotnet CoachFlow.Api.dll invite <E-Mail>
public static class AccountCommands
{
    public static async Task<int> InviteAsync(IServiceProvider services, string email, TextWriter output)
    {
        if (!InvitationService.TryNormalizeEmail(email, out var normalized))
        {
            await output.WriteLineAsync("Die E-Mail-Adresse ist ungültig.");
            return 1;
        }

        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        if (db.Users.Any(u => u.NormalizedEmail == normalized.ToUpperInvariant()))
        {
            await output.WriteLineAsync("Für diese E-Mail-Adresse gibt es bereits ein Konto.");
            return 1;
        }

        var options = scope.ServiceProvider.GetRequiredService<IOptions<AccountOptions>>().Value;
        var token = await scope.ServiceProvider.GetRequiredService<InvitationService>().CreateAsync(normalized, invitedById: null);
        var path = InvitationService.InvitationPath(token);

        await output.WriteLineAsync($"Einladung für {normalized} angelegt. Gültig {AccountMails.FormatLifetime(options.InvitationLifetime)}.");
        await output.WriteLineAsync(string.IsNullOrEmpty(options.PublicBaseUrl)
            ? $"Link (an die Adresse der App anhängen): {path}"
            : $"Link: {options.Link(path)}");
        return 0;
    }
}
