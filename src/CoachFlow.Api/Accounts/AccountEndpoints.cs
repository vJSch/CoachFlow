using System.Security.Claims;
using CoachFlow.Api.Data;
using CoachFlow.Api.Mail;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace CoachFlow.Api.Accounts;

public record AccountResponse(string Email, string Name);

public record LoginRequest(string? Email, string? Password);

public record RegisterRequest(string? Token, string? Name, string? Password);

public record InvitationInfoRequest(string? Token);

public record InvitationInfoResponse(string Email);

public record InviteRequest(string? Email);

public record ForgotPasswordRequest(string? Email);

public record ResetPasswordRequest(string? Email, string? Token, string? Password);

public static class AccountEndpoints
{
    private const int MaxNameLength = 100;
    private const string InvalidInvitation = "Die Einladung ist ungültig oder abgelaufen. Bitte lass dich erneut einladen.";
    private const string InvalidResetLink = "Der Link ist ungültig oder abgelaufen. Bitte fordere einen neuen an.";
    private const string MailNotConfigured = "Der E-Mail-Versand ist noch nicht eingerichtet.";

    public static void MapCoachFlowAccounts(this IEndpointRouteBuilder api)
    {
        var account = api.MapGroup("/account");
        account.MapPost("/login", LoginAsync).RequireRateLimiting(AccountSetup.AuthRateLimit);
        account.MapPost("/logout", LogoutAsync);
        account.MapGet("/me", MeAsync).RequireAuthorization();
        account.MapPost("/invitation", InvitationInfoAsync).RequireRateLimiting(AccountSetup.AuthRateLimit);
        account.MapPost("/register", RegisterAsync).RequireRateLimiting(AccountSetup.AuthRateLimit);
        account.MapPost("/forgot-password", ForgotPasswordAsync).RequireRateLimiting(AccountSetup.AuthRateLimit);
        account.MapPost("/reset-password", ResetPasswordAsync).RequireRateLimiting(AccountSetup.AuthRateLimit);

        api.MapPost("/invitations", InviteAsync).RequireAuthorization();
    }

    private static async Task<IResult> LoginAsync(
        LoginRequest request, UserManager<AppUser> users, SignInManager<AppUser> signIn)
    {
        const string wrongCredentials = "E-Mail-Adresse oder Passwort ist falsch.";
        var user = InvitationService.TryNormalizeEmail(request.Email, out var email)
            ? await users.FindByEmailAsync(email)
            : null;
        if (user is null || !user.IsActive || string.IsNullOrEmpty(request.Password))
        {
            return Problem(StatusCodes.Status401Unauthorized, wrongCredentials);
        }

        var result = await signIn.PasswordSignInAsync(user, request.Password, isPersistent: true, lockoutOnFailure: true);
        if (result.IsLockedOut)
        {
            return Problem(StatusCodes.Status429TooManyRequests,
                "Zu viele Fehlversuche. Bitte versuche es in 15 Minuten erneut oder setze dein Passwort zurück.");
        }

        return result.Succeeded
            ? Results.Ok(ToResponse(user))
            : Problem(StatusCodes.Status401Unauthorized, wrongCredentials);
    }

    private static async Task<IResult> LogoutAsync(SignInManager<AppUser> signIn)
    {
        await signIn.SignOutAsync();
        return Results.NoContent();
    }

    private static async Task<IResult> MeAsync(ClaimsPrincipal principal, UserManager<AppUser> users)
    {
        var user = await users.GetUserAsync(principal);
        return user is { IsActive: true } ? Results.Ok(ToResponse(user)) : Results.Unauthorized();
    }

    private static async Task<IResult> InvitationInfoAsync(InvitationInfoRequest request, InvitationService invitations)
    {
        var invitation = await invitations.FindOpenAsync(request.Token);
        return invitation is null
            ? Problem(StatusCodes.Status400BadRequest, InvalidInvitation)
            : Results.Ok(new InvitationInfoResponse(invitation.Email));
    }

    private static async Task<IResult> RegisterAsync(
        RegisterRequest request,
        AppDbContext db,
        InvitationService invitations,
        UserManager<AppUser> users,
        SignInManager<AppUser> signIn)
    {
        var name = request.Name?.Trim() ?? "";
        if (name.Length is 0 or > MaxNameLength)
        {
            return Problem(StatusCodes.Status400BadRequest, $"Bitte gib einen Namen mit höchstens {MaxNameLength} Zeichen an.");
        }

        await using var transaction = await db.Database.BeginTransactionAsync();
        var invitation = await invitations.FindOpenAsync(request.Token);
        if (invitation is null)
        {
            return Problem(StatusCodes.Status400BadRequest, InvalidInvitation);
        }

        var user = new AppUser
        {
            UserName = invitation.Email,
            Email = invitation.Email,
            // Wer den Link aus der E-Mail nutzt, hat die Adresse damit bestätigt.
            EmailConfirmed = true,
            Name = name
        };
        var result = await users.CreateAsync(user, request.Password ?? "");
        if (!result.Succeeded)
        {
            return IdentityProblem(result);
        }

        invitations.MarkRedeemed(invitation);
        await db.SaveChangesAsync();
        await transaction.CommitAsync();

        await signIn.SignInAsync(user, isPersistent: true);
        return Results.Ok(ToResponse(user));
    }

    private static async Task<IResult> InviteAsync(
        InviteRequest request,
        ClaimsPrincipal principal,
        UserManager<AppUser> users,
        InvitationService invitations,
        IMailSender mail,
        IOptions<AccountOptions> options,
        ILogger<InvitationService> logger)
    {
        if (!mail.IsConfigured)
        {
            return Problem(StatusCodes.Status503ServiceUnavailable, MailNotConfigured);
        }

        if (!InvitationService.TryNormalizeEmail(request.Email, out var email))
        {
            return Problem(StatusCodes.Status400BadRequest, "Bitte gib eine gültige E-Mail-Adresse an.");
        }

        if (await users.FindByEmailAsync(email) is not null)
        {
            return Problem(StatusCodes.Status409Conflict, "Für diese E-Mail-Adresse gibt es bereits ein Konto.");
        }

        var inviter = await users.GetUserAsync(principal);
        if (inviter is not { IsActive: true })
        {
            return Results.Unauthorized();
        }

        var token = await invitations.CreateAsync(email, inviter.Id);
        var link = options.Value.Link(InvitationService.InvitationPath(token));
        try
        {
            await mail.SendAsync(AccountMails.Invitation(email, inviter.Name, link, options.Value.InvitationLifetime));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Einladung konnte nicht gesendet werden.");
            return Problem(StatusCodes.Status502BadGateway, "Die E-Mail konnte nicht gesendet werden. Bitte versuche es später erneut.");
        }

        return Results.Accepted();
    }

    private static async Task<IResult> ForgotPasswordAsync(
        ForgotPasswordRequest request,
        UserManager<AppUser> users,
        IMailSender mail,
        IOptions<AccountOptions> options,
        ILogger<InvitationService> logger)
    {
        // Vor der Suche geprüft, damit die Antwort nicht verrät, ob ein Konto existiert.
        if (!mail.IsConfigured)
        {
            return Problem(StatusCodes.Status503ServiceUnavailable, MailNotConfigured);
        }

        if (InvitationService.TryNormalizeEmail(request.Email, out var email)
            && await users.FindByEmailAsync(email) is { IsActive: true } user)
        {
            var token = await users.GeneratePasswordResetTokenAsync(user);
            var link = options.Value.Link(
                $"/passwort-neu#email={Uri.EscapeDataString(email)}&token={Uri.EscapeDataString(token)}");
            try
            {
                await mail.SendAsync(AccountMails.PasswordReset(email, link, options.Value.PasswordResetLifetime));
            }
            catch (Exception ex)
            {
                // Bewusst dieselbe Antwort wie bei unbekannten Adressen.
                logger.LogError(ex, "E-Mail zum Zurücksetzen des Passworts konnte nicht gesendet werden.");
            }
        }

        return Results.Accepted();
    }

    private static async Task<IResult> ResetPasswordAsync(ResetPasswordRequest request, UserManager<AppUser> users)
    {
        var user = InvitationService.TryNormalizeEmail(request.Email, out var email)
            ? await users.FindByEmailAsync(email)
            : null;
        if (user is not { IsActive: true } || string.IsNullOrEmpty(request.Token))
        {
            return Problem(StatusCodes.Status400BadRequest, InvalidResetLink);
        }

        // Das neue Passwort ändert den Sicherheitsstempel. Dadurch wird der Token ungültig und lässt sich nur einmal nutzen.
        var result = await users.ResetPasswordAsync(user, request.Token, request.Password ?? "");
        if (!result.Succeeded)
        {
            return result.Errors.Any(e => e.Code == nameof(IdentityErrorDescriber.InvalidToken))
                ? Problem(StatusCodes.Status400BadRequest, InvalidResetLink)
                : IdentityProblem(result);
        }

        await users.ResetAccessFailedCountAsync(user);
        await users.SetLockoutEndDateAsync(user, null);
        return Results.NoContent();
    }

    private static AccountResponse ToResponse(AppUser user) => new(user.Email!, user.Name);

    private static IResult Problem(int status, string detail) => Results.Problem(detail: detail, statusCode: status);

    private static IResult IdentityProblem(IdentityResult result) =>
        Problem(StatusCodes.Status400BadRequest, string.Join(" ", result.Errors.Select(e => e.Description)));
}
