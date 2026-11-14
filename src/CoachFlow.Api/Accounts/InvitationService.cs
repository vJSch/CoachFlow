using System.Buffers.Text;
using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using CoachFlow.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CoachFlow.Api.Accounts;

public sealed class InvitationService(AppDbContext db, TimeProvider time, IOptions<AccountOptions> options)
{
    public static bool TryNormalizeEmail(string? email, out string normalized)
    {
        normalized = email?.Trim().ToLowerInvariant() ?? "";
        // Schließt Formen wie "Name <a@b.de>" aus, die MailAddress sonst akzeptiert.
        return MailAddress.TryCreate(normalized, out var address) && address.Address == normalized;
    }

    // Liefert den Token im Klartext. Er wird nur für den Link verwendet und nicht gespeichert.
    public async Task<string> CreateAsync(string normalizedEmail, string? invitedById, CancellationToken ct = default)
    {
        // Eine neue Einladung macht ältere, noch offene Einladungen derselben Adresse ungültig.
        await db.Invitations
            .Where(i => i.Email == normalizedEmail && i.RedeemedAt == null)
            .ExecuteDeleteAsync(ct);

        var token = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32));
        db.Invitations.Add(new Invitation
        {
            Id = Guid.NewGuid(),
            Email = normalizedEmail,
            TokenHash = Hash(token),
            ExpiresAt = Now() + options.Value.InvitationLifetime,
            InvitedById = invitedById
        });
        await db.SaveChangesAsync(ct);

        return token;
    }

    public Task<Invitation?> FindOpenAsync(string? token, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return Task.FromResult<Invitation?>(null);
        }

        var hash = Hash(token);
        var now = Now();
        return db.Invitations.FirstOrDefaultAsync(
            i => i.TokenHash == hash && i.RedeemedAt == null && i.ExpiresAt > now, ct);
    }

    public void MarkRedeemed(Invitation invitation) => invitation.RedeemedAt = Now();

    public static string InvitationPath(string token) => $"/einladung#token={token}";

    private DateTime Now() => time.GetUtcNow().UtcDateTime;

    private static string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
