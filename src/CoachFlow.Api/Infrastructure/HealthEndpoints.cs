using Microsoft.AspNetCore.Diagnostics.HealthChecks;

namespace CoachFlow.Api.Infrastructure;

public static class HealthEndpoints
{
    public const string ReadyTag = "ready";

    public static void MapCoachFlowHealthChecks(this WebApplication app)
    {
        // Liveness prüft nur den Prozess, Readiness zusätzlich die Datenbank.
        app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
        app.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains(ReadyTag)
        });
    }
}
