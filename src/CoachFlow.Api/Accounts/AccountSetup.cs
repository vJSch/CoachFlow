using System.Threading.RateLimiting;
using CoachFlow.Api.Data;
using CoachFlow.Api.Mail;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CoachFlow.Api.Accounts;

public static class AccountSetup
{
    public const string AuthRateLimit = "auth";

    public static void AddCoachFlowAccounts(this WebApplicationBuilder builder)
    {
        var services = builder.Services;
        var accountSection = builder.Configuration.GetSection(AccountOptions.Section);
        var accountOptions = accountSection.Get<AccountOptions>() ?? new AccountOptions();
        services.Configure<AccountOptions>(accountSection);
        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<InvitationService>();

        services.AddIdentityCore<AppUser>(options =>
            {
                // Länge statt Zeichenregeln, wie es aktuelle Empfehlungen (NIST SP 800-63B) vorsehen.
                options.Password.RequiredLength = 10;
                options.Password.RequireDigit = false;
                options.Password.RequireLowercase = false;
                options.Password.RequireUppercase = false;
                options.Password.RequireNonAlphanumeric = false;
                options.Password.RequiredUniqueChars = 1;
                options.User.RequireUniqueEmail = true;
                // Die E-Mail-Adresse dient als Benutzername. Ein leerer Wert schaltet die Zeichenprüfung ab.
                options.User.AllowedUserNameCharacters = "";
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            })
            .AddEntityFrameworkStores<AppDbContext>()
            .AddSignInManager()
            .AddDefaultTokenProviders()
            .AddErrorDescriber<GermanIdentityErrorDescriber>();

        services.Configure<DataProtectionTokenProviderOptions>(options =>
            options.TokenLifespan = accountOptions.PasswordResetLifetime);

        // Schlüssel in der Datenbank, damit Anmeldungen und Reset-Links einen Neustart überstehen.
        services.AddDataProtection()
            .SetApplicationName("CoachFlow")
            .PersistKeysToDbContext<AppDbContext>();

        services.AddAuthentication(IdentityConstants.ApplicationScheme).AddIdentityCookies();
        var plainHttpAllowed = builder.Environment.IsDevelopment() || builder.Environment.IsEnvironment("Testing");
        services.ConfigureApplicationCookie(options =>
        {
            options.Cookie.Name = "coachflow.auth";
            options.Cookie.HttpOnly = true;
            // Strict und reine JSON-Endpunkte schützen gegen Anfragen von fremden Seiten (CSRF).
            options.Cookie.SameSite = SameSiteMode.Strict;
            options.Cookie.SecurePolicy = plainHttpAllowed ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
            options.ExpireTimeSpan = TimeSpan.FromDays(14);
            options.SlidingExpiration = true;
            // Die API antwortet mit Statuscodes statt mit Weiterleitungen auf eine Anmeldeseite.
            options.Events.OnRedirectToLogin = context =>
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return Task.CompletedTask;
            };
            options.Events.OnRedirectToAccessDenied = context =>
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return Task.CompletedTask;
            };
        });
        services.AddAuthorization();

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            // Hinter einem Proxy ist das die Adresse des Proxys. Das muss beim Hosting geklärt werden.
            options.AddPolicy(AuthRateLimit, context => RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "unbekannt",
                _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1) }));
        });

        var smtpSection = builder.Configuration.GetSection(SmtpOptions.Section);
        services.Configure<SmtpOptions>(smtpSection);
        if (!string.IsNullOrEmpty(smtpSection["Host"]))
        {
            if (string.IsNullOrEmpty(accountOptions.PublicBaseUrl))
            {
                throw new InvalidOperationException("Accounts:PublicBaseUrl muss gesetzt sein, wenn E-Mails versendet werden.");
            }

            services.TryAddSingleton<IMailSender, SmtpMailSender>();
        }
        else if (builder.Environment.IsDevelopment())
        {
            services.TryAddSingleton<IMailSender, LogMailSender>();
        }
        else
        {
            services.TryAddSingleton<IMailSender, UnconfiguredMailSender>();
        }
    }
}
