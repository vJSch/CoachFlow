using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using CoachFlow.AiAdapters.Canned;
using CoachFlow.Api;
using CoachFlow.Application.Ai;
using CoachFlow.Application.Plans;
using CoachFlow.Export.Xlsx;
using CoachFlow.Infrastructure;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;

var builder = WebApplication.CreateBuilder(args);
var config = builder.Configuration;

var databaseProvider = config.GetValue("Database:Provider", DatabaseProvider.Postgres);
var connectionString = config.GetConnectionString("CoachFlow")
    ?? throw new InvalidOperationException("ConnectionStrings:CoachFlow fehlt.");

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddCoachFlowInfrastructure(databaseProvider, connectionString);
builder.Services.AddCoachFlowAi(config);
builder.Services.AddSingleton<IPlanExporter, XlsxPlanExporter>();

builder.Services.AddProblemDetails();
builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddAuthorization();
builder.Services
    .AddIdentityApiEndpoints<CoachFlowUser>(options =>
    {
        options.User.RequireUniqueEmail = true;
        // Länge statt Zeichenregeln (NIST SP 800-63B): Pflicht-Sonderzeichen führen eher zu vorhersehbaren Mustern.
        options.Password.RequiredLength = 10;
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequireUppercase = false;
        options.Password.RequireLowercase = false;
        options.Password.RequireDigit = false;
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        // Ohne E-Mail-Dienst könnte niemand ein Konto bestätigen. Vor dem Start mit Mailversand wieder einschalten.
        options.SignIn.RequireConfirmedEmail = false;
    })
    .AddEntityFrameworkStores<CoachFlowDbContext>();
builder.Services.AddTransient<IEmailSender<CoachFlowUser>, LoggingEmailSender>();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.Cookie.SecurePolicy = builder.Environment.IsProduction()
        ? CookieSecurePolicy.Always
        : CookieSecurePolicy.SameAsRequest;
    options.ExpireTimeSpan = TimeSpan.FromDays(14);
    options.SlidingExpiration = true;
    // Eine API antwortet mit Statuscodes statt auf eine Anmeldeseite umzuleiten.
    options.Events.OnRedirectToLogin = context => { context.Response.StatusCode = StatusCodes.Status401Unauthorized; return Task.CompletedTask; };
    options.Events.OnRedirectToAccessDenied = context => { context.Response.StatusCode = StatusCodes.Status403Forbidden; return Task.CompletedTask; };
});

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    // Bremst das Durchprobieren von Passwörtern; zusätzlich sperrt Identity ein Konto nach fünf Fehlversuchen.
    options.AddPolicy("auth", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unbekannt",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1) }));
});

var app = builder.Build();

await using (var scope = app.Services.CreateAsyncScope())
{
    var db = scope.ServiceProvider.GetRequiredService<CoachFlowDbContext>();
    await db.PrepareDatabaseAsync(databaseProvider, CancellationToken.None);
}

app.UseExceptionHandler();
app.UseStatusCodePages();
if (app.Environment.IsDevelopment()) app.UseWebAssemblyDebugging();

// Die Blazor-Oberfläche liegt auf demselben Ursprung wie die API (Cookie mit SameSite=Strict).
// MapStaticAssets liefert auch _framework aus; UseBlazorFrameworkFiles würde damit kollidieren.
app.MapStaticAssets();

app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapGroup("/api/auth").MapIdentityApi<CoachFlowUser>().RequireRateLimiting("auth");
app.MapPost("/api/auth/logout", async (SignInManager<CoachFlowUser> signIn) =>
{
    await signIn.SignOutAsync();
    return TypedResults.NoContent();
}).RequireAuthorization();

app.MapCoachFlowEndpoints();

app.MapGet("/health", async Task<Results<Ok<string>, StatusCodeHttpResult>> (CoachFlowDbContext db, CancellationToken ct) =>
    await db.Database.CanConnectAsync(ct) ? TypedResults.Ok("ok") : TypedResults.StatusCode(StatusCodes.Status503ServiceUnavailable));

// Unbekannte API-Pfade bleiben 404; alle übrigen Pfade sind Seiten der Oberfläche.
app.MapFallback("/api/{**rest}", () => Results.NotFound(new ApiError("not-found", "Der Eintrag wurde nicht gefunden.")));
app.MapFallbackToFile("index.html");

app.Run();

public partial class Program;

internal static class AiSetup
{
    public static IServiceCollection AddCoachFlowAi(this IServiceCollection services, IConfiguration config)
    {
        var provider = config.GetValue("Ai:Provider", "None");
        var options = new AiOptions
        {
            // Ohne Anbieter gilt die KI als abgeschaltet; Budget 0 bewirkt genau das.
            MonthlyBudget = provider == "None" ? 0m : config.GetValue("Ai:MonthlyBudgetEuro", 0m),
            ServerDeadline = config.GetValue("Ai:ServerDeadline", TimeSpan.FromSeconds(13)),
            MaxRequestsPerMinutePerUser = config.GetValue("Ai:MaxRequestsPerMinutePerUser", 5)
        };
        services.AddSingleton(options);

        IExerciseSuggestionProvider implementation = provider switch
        {
            "None" or "Canned" => CannedSuggestionProvider.WithValidAnswer(),
            _ => throw new InvalidOperationException($"Unbekannter KI-Anbieter: {provider}")
        };
        services.AddSingleton(implementation);
        return services;
    }
}
