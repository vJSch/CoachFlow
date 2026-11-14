using System.Net;
using System.Net.Http.Json;
using CoachFlow.Api.Accounts;
using CoachFlow.Api.Data;
using Microsoft.Extensions.DependencyInjection;

namespace CoachFlow.Api.Tests;

public class AccountTests
{
    private const string Password = TestApp.Password;

    [Fact]
    public async Task Api_rejects_requests_without_login_with_401()
    {
        await using var app = new TestApp();
        var client = app.CreateClient();

        var me = await client.GetAsync("/api/account/me");
        var invite = await client.PostAsJsonAsync("/api/invitations", new { email = "neu@example.org" });

        Assert.Equal(HttpStatusCode.Unauthorized, me.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, invite.StatusCode);
    }

    [Fact]
    public async Task Register_without_valid_invitation_is_rejected()
    {
        await using var app = new TestApp();

        var response = await app.CreateClient().PostAsJsonAsync("/api/account/register",
            new { token = "ausgedacht", name = "Eindringling", password = Password });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("Einladung ist ungültig", await TestApp.DetailAsync(response));
    }

    [Fact]
    public async Task First_account_via_command_registers_and_is_logged_in()
    {
        await using var app = new TestApp();
        var token = await app.InviteViaCommandAsync(" Erste@Example.org ");
        var client = app.CreateClient();

        var info = await client.PostAsJsonAsync("/api/account/invitation", new { token });
        var register = await client.PostAsJsonAsync("/api/account/register",
            new { token, name = "Erste Trainerin", password = Password });
        var me = await client.GetFromJsonAsync<AccountResponse>("/api/account/me");

        Assert.Equal("erste@example.org", (await info.Content.ReadFromJsonAsync<InvitationInfoResponse>())!.Email);
        Assert.Equal(HttpStatusCode.OK, register.StatusCode);
        Assert.Equal(new AccountResponse("erste@example.org", "Erste Trainerin"), me);
    }

    [Fact]
    public async Task Invitation_works_only_once()
    {
        await using var app = new TestApp();
        var token = await app.InviteViaCommandAsync("einmal@example.org");
        var client = app.CreateClient();

        await client.PostAsJsonAsync("/api/account/register", new { token, name = "A", password = Password });
        var second = await app.CreateClient().PostAsJsonAsync("/api/account/register",
            new { token, name = "B", password = Password });

        Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);
    }

    [Fact]
    public async Task Invitation_expires_after_three_days()
    {
        await using var app = new TestApp();
        var token = await app.InviteViaCommandAsync("spaet@example.org");
        app.Time.Advance(TimeSpan.FromDays(3) + TimeSpan.FromMinutes(1));

        var response = await app.CreateClient().PostAsJsonAsync("/api/account/register",
            new { token, name = "Zu spät", password = Password });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task New_invitation_invalidates_older_one_for_same_address()
    {
        await using var app = new TestApp();
        var older = await app.InviteViaCommandAsync("doppelt@example.org");
        var newer = await app.InviteViaCommandAsync("doppelt@example.org");
        var client = app.CreateClient();

        var withOlder = await client.PostAsJsonAsync("/api/account/invitation", new { token = older });
        var withNewer = await client.PostAsJsonAsync("/api/account/invitation", new { token = newer });

        Assert.Equal(HttpStatusCode.BadRequest, withOlder.StatusCode);
        Assert.Equal(HttpStatusCode.OK, withNewer.StatusCode);
    }

    [Fact]
    public async Task Logged_in_user_invites_by_mail_and_invitee_registers()
    {
        await using var app = new TestApp();
        var inviter = await app.CreateUserAsync("trainer@example.org", "Toni Trainer");

        var invite = await inviter.PostAsJsonAsync("/api/invitations", new { email = "neu@example.org" });
        var mail = Assert.Single(app.Mail.Sent);
        var token = TestApp.InvitationToken(mail.Body);
        var register = await app.CreateClient().PostAsJsonAsync("/api/account/register",
            new { token, name = "Neu", password = Password });

        Assert.Equal(HttpStatusCode.Accepted, invite.StatusCode);
        Assert.Equal("neu@example.org", mail.To);
        Assert.Contains("Toni Trainer", mail.Body);
        Assert.Contains("https://coachflow.test/einladung#token=", mail.Body);
        Assert.Equal(HttpStatusCode.OK, register.StatusCode);
    }

    [Fact]
    public async Task Inviting_existing_account_is_a_conflict()
    {
        await using var app = new TestApp();
        var client = await app.CreateUserAsync("vorhanden@example.org");

        var response = await client.PostAsJsonAsync("/api/invitations", new { email = "VORHANDEN@example.org" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Empty(app.Mail.Sent);
    }

    [Fact]
    public async Task Inviting_without_mail_configuration_returns_503()
    {
        await using var app = new TestApp(mailConfigured: false);
        var client = await app.CreateUserAsync("trainer@example.org");

        var response = await client.PostAsJsonAsync("/api/invitations", new { email = "neu@example.org" });

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    [Fact]
    public async Task Short_password_is_rejected_with_german_message()
    {
        await using var app = new TestApp();
        var token = await app.InviteViaCommandAsync("kurz@example.org");

        var response = await app.CreateClient().PostAsJsonAsync("/api/account/register",
            new { token, name = "Kurz", password = "123456789" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("Das Passwort muss mindestens 10 Zeichen lang sein.", await TestApp.DetailAsync(response));
    }

    [Fact]
    public async Task Login_and_logout()
    {
        await using var app = new TestApp();
        await app.CreateUserAsync("login@example.org");
        var client = app.CreateClient();

        var wrong = await client.PostAsJsonAsync("/api/account/login", new { email = "login@example.org", password = "falsch-falsch" });
        var right = await client.PostAsJsonAsync("/api/account/login", new { email = "Login@Example.org", password = Password });
        var meLoggedIn = await client.GetAsync("/api/account/me");
        await client.PostAsync("/api/account/logout", null);
        var meLoggedOut = await client.GetAsync("/api/account/me");

        Assert.Equal(HttpStatusCode.Unauthorized, wrong.StatusCode);
        Assert.Equal(HttpStatusCode.OK, right.StatusCode);
        Assert.Equal(HttpStatusCode.OK, meLoggedIn.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, meLoggedOut.StatusCode);
    }

    [Fact]
    public async Task Account_is_locked_after_five_failed_attempts()
    {
        await using var app = new TestApp();
        await app.CreateUserAsync("gesperrt@example.org");
        var client = app.CreateClient();

        for (var i = 0; i < 5; i++)
        {
            await client.PostAsJsonAsync("/api/account/login", new { email = "gesperrt@example.org", password = "falsch-falsch" });
        }

        var withRightPassword = await client.PostAsJsonAsync("/api/account/login",
            new { email = "gesperrt@example.org", password = Password });

        Assert.Equal(HttpStatusCode.TooManyRequests, withRightPassword.StatusCode);
    }

    [Fact]
    public async Task Inactive_account_cannot_log_in()
    {
        await using var app = new TestApp();
        await app.CreateUserAsync("inaktiv@example.org");
        using (var scope = app.Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Users.Single().IsActive = false;
            await db.SaveChangesAsync();
        }

        var response = await app.CreateClient().PostAsJsonAsync("/api/account/login",
            new { email = "inaktiv@example.org", password = Password });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Forgot_password_does_not_reveal_unknown_addresses()
    {
        await using var app = new TestApp();

        var response = await app.CreateClient().PostAsJsonAsync("/api/account/forgot-password",
            new { email = "unbekannt@example.org" });

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.Empty(app.Mail.Sent);
    }

    [Fact]
    public async Task Password_reset_works_once_and_replaces_old_password()
    {
        await using var app = new TestApp();
        await app.CreateUserAsync("reset@example.org");
        var client = app.CreateClient();
        const string newPassword = "ganz-neues-passwort";

        await client.PostAsJsonAsync("/api/account/forgot-password", new { email = "reset@example.org" });
        var (email, token) = TestApp.ResetLink(Assert.Single(app.Mail.Sent).Body);
        var reset = await client.PostAsJsonAsync("/api/account/reset-password", new { email, token, password = newPassword });
        var again = await client.PostAsJsonAsync("/api/account/reset-password", new { email, token, password = "noch-ein-passwort" });
        var oldLogin = await client.PostAsJsonAsync("/api/account/login", new { email, password = Password });
        var newLogin = await client.PostAsJsonAsync("/api/account/login", new { email, password = newPassword });

        Assert.Equal("reset@example.org", email);
        Assert.Equal(HttpStatusCode.NoContent, reset.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, again.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, oldLogin.StatusCode);
        Assert.Equal(HttpStatusCode.OK, newLogin.StatusCode);
    }

    [Fact]
    public async Task Password_reset_link_expires_after_configured_lifetime()
    {
        // Identity prüft den Reset-Token mit der Systemuhr, deshalb hier eine kurze Gültigkeit und echtes Warten.
        await using var app = new TestApp(settings: new Dictionary<string, string>
        {
            ["Accounts:PasswordResetLifetime"] = "00:00:01"
        });
        await app.CreateUserAsync("ablauf@example.org");
        var client = app.CreateClient();

        await client.PostAsJsonAsync("/api/account/forgot-password", new { email = "ablauf@example.org" });
        var (email, token) = TestApp.ResetLink(Assert.Single(app.Mail.Sent).Body);
        await Task.Delay(TimeSpan.FromSeconds(2.5));
        var reset = await client.PostAsJsonAsync("/api/account/reset-password", new { email, token, password = "ganz-neues-passwort" });

        Assert.Equal(HttpStatusCode.BadRequest, reset.StatusCode);
    }

    [Fact]
    public async Task Login_is_rate_limited()
    {
        await using var app = new TestApp();
        var client = app.CreateClient();

        HttpResponseMessage? last = null;
        for (var i = 0; i < 11; i++)
        {
            last = await client.PostAsJsonAsync("/api/account/login", new { email = $"x{i}@example.org", password = "egal-egal-egal" });
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, last!.StatusCode);
    }
}
