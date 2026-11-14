using System.Text.Json.Serialization;
using CoachFlow.Api.Accounts;
using CoachFlow.Api.Data;
using CoachFlow.Api.Exercises;
using CoachFlow.Api.Infrastructure;
using CoachFlow.Api.Plans;
using CoachFlow.Api.Suggestions;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var connectionString = SqliteDatabase.ResolveConnectionString(
    builder.Configuration.GetConnectionString("Default")
        ?? throw new InvalidOperationException("ConnectionStrings:Default ist nicht gesetzt."),
    builder.Environment.ContentRootPath);

builder.Services.AddDbContext<AppDbContext>(options => options.UseSqlite(connectionString));
builder.Services.AddHealthChecks().AddDbContextCheck<AppDbContext>(tags: [HealthEndpoints.ReadyTag]);
builder.Services.AddProblemDetails();
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.AddCoachFlowAccounts();
builder.AddCoachFlowSuggestions();

var app = builder.Build();

if (app.Configuration.GetValue<bool>("Database:MigrateOnStartup"))
{
    using var scope = app.Services.CreateScope();
    await SqliteDatabase.MigrateAsync(scope.ServiceProvider.GetRequiredService<AppDbContext>());
}

if (args is ["invite", var inviteEmail])
{
    return await AccountCommands.InviteAsync(app.Services, inviteEmail, Console.Out);
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler();
}

app.UseCoachFlowSpa();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapCoachFlowHealthChecks();
var api = app.MapGroup("/api");
api.MapCoachFlowAccounts();
api.MapCoachFlowExercises();
api.MapCoachFlowPlans();
api.MapCoachFlowSuggestions();
app.MapCoachFlowSpaFallback();

await app.RunAsync();
return 0;

public partial class Program;
