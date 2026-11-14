using CoachFlow.Application;
using CoachFlow.Application.Ai;
using CoachFlow.Application.Exercises;
using CoachFlow.Application.Plans;
using CoachFlow.Application.Suggestions;
using CoachFlow.Infrastructure.Budget;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.DependencyInjection;

namespace CoachFlow.Infrastructure;

public enum DatabaseProvider
{
    /// <summary>Für den Betrieb. Das Schema entsteht über Migrationen.</summary>
    Postgres,

    /// <summary>Nur für lokale Entwicklung und Tests ohne Docker. Das Schema entsteht ohne Migrationen.</summary>
    Sqlite
}

public static class InfrastructureSetup
{
    public static IServiceCollection AddCoachFlowInfrastructure(
        this IServiceCollection services,
        DatabaseProvider provider,
        string connectionString)
    {
        services.AddDbContext<CoachFlowDbContext>(options => _ = provider switch
        {
            DatabaseProvider.Postgres => options.UseNpgsql(connectionString),
            DatabaseProvider.Sqlite => options.UseSqlite(connectionString),
            _ => throw new ArgumentOutOfRangeException(nameof(provider))
        });

        services.AddScoped<IUnitOfWork, EfUnitOfWork>();
        services.AddScoped<IExerciseRepository, EfExerciseRepository>();
        services.AddScoped<IFocusAreaRepository, EfFocusAreaRepository>();
        services.AddScoped<IPlanRepository, EfPlanRepository>();
        services.AddScoped<ISuggestionRepository, EfSuggestionRepository>();
        services.AddScoped<IAiBudgetLedger, EfAiBudgetLedger>();
        services.AddScoped<EfAiBudgetLedger>();

        services.AddScoped<ExerciseService>();
        services.AddScoped<PlanService>();
        services.AddScoped<SuggestionService>();
        services.AddScoped<AiBudgetGuard>();
        services.AddSingleton<AiRequestGate>();
        return services;
    }

    /// <summary>Migrationen gibt es nur für PostgreSQL; SQLite erzeugt das Schema direkt.</summary>
    public static async Task PrepareDatabaseAsync(this CoachFlowDbContext db, DatabaseProvider provider, CancellationToken cancellationToken)
    {
        if (provider == DatabaseProvider.Postgres) await db.Database.MigrateAsync(cancellationToken);
        else await db.Database.EnsureCreatedAsync(cancellationToken);
    }
}

/// <summary>Nur für <c>dotnet ef migrations</c>. Beim Erzeugen einer Migration wird keine Verbindung aufgebaut.</summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<CoachFlowDbContext>
{
    public CoachFlowDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<CoachFlowDbContext>()
            .UseNpgsql("Host=localhost;Database=coachflow_design;Username=design;Password=design")
            .Options);
}
