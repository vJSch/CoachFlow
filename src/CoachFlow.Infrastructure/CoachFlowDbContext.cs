using System.Text.Json;
using System.Text.Json.Serialization;
using CoachFlow.Application.Exercises;
using CoachFlow.Application.Plans;
using CoachFlow.Application.Suggestions;
using CoachFlow.Domain;
using CoachFlow.Infrastructure.Budget;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoachFlow.Infrastructure;

/// <summary>Konto ohne Rollen (Entscheidung A2). Die Id ist die Nutzer-Id in Plänen und Vorschlägen.</summary>
public sealed class CoachFlowUser : IdentityUser<Guid>;

/// <summary>
/// <see cref="IdentityUserContext{TUser,TKey}"/> statt IdentityDbContext: Es entstehen keine
/// Rollentabellen, weil der MVP keine unterschiedlichen Berechtigungen kennt.
/// </summary>
public sealed class CoachFlowDbContext(DbContextOptions<CoachFlowDbContext> options)
    : IdentityUserContext<CoachFlowUser, Guid>(options)
{
    /// <summary>Enums als Text, damit gespeicherte Daten lesbar bleiben und eine neue Enum-Reihenfolge sie nicht verändert.</summary>
    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public DbSet<Exercise> Exercises => Set<Exercise>();
    public DbSet<FocusArea> FocusAreas => Set<FocusArea>();
    public DbSet<TrainingPlan> Plans => Set<TrainingPlan>();
    public DbSet<AiSuggestion> Suggestions => Set<AiSuggestion>();
    public DbSet<BudgetMonth> BudgetMonths => Set<BudgetMonth>();
    public DbSet<BudgetReservation> BudgetReservations => Set<BudgetReservation>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Exercise>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Content).AsJson();
            e.Property(x => x.Origin).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.Version).IsConcurrencyToken();
            e.Ignore(x => x.IsArchived);
            e.HasIndex(x => x.ArchivedAt);
        });

        modelBuilder.Entity<FocusArea>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(60).IsRequired();
        });

        modelBuilder.Entity<TrainingPlan>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Title).HasMaxLength(PlanService.MaxTitleLength).IsRequired();
            e.Property(x => x.Parameters).AsJson();
            e.Property(x => x.Items).AsJson();
            e.Property(x => x.Version).IsConcurrencyToken();
            e.Ignore(x => x.TotalMinutes);
            e.Ignore(x => x.Findings);
            e.HasIndex(x => x.OwnerId);
        });

        modelBuilder.Entity<AiSuggestion>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Parameters).AsJson();
            e.Property(x => x.Draft).AsJson();
            e.Property(x => x.ProviderName).HasMaxLength(60).IsRequired();
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.Version).IsConcurrencyToken();
            e.Ignore(x => x.Findings);
            e.HasIndex(x => x.UserId);
        });

        modelBuilder.Entity<BudgetMonth>(e =>
        {
            e.HasKey(x => x.Month);
            e.Property(x => x.Month).HasMaxLength(7);
        });

        modelBuilder.Entity<BudgetReservation>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Month).HasMaxLength(7);
            e.HasIndex(x => x.Month);
        });
    }
}

internal static class JsonColumnExtensions
{
    /// <summary>
    /// Speichert verschachtelte Fachobjekte als JSON-Text. Gesucht wird darin nicht in SQL;
    /// bei der erwarteten Datenmenge genügt das Filtern im Speicher.
    /// </summary>
    public static PropertyBuilder<T> AsJson<T>(this PropertyBuilder<T> property)
    {
        property
            .HasConversion(
                value => JsonSerializer.Serialize(value, CoachFlowDbContext.Json),
                text => JsonSerializer.Deserialize<T>(text, CoachFlowDbContext.Json)!,
                new ValueComparer<T>(
                    (a, b) => JsonSerializer.Serialize(a, CoachFlowDbContext.Json) == JsonSerializer.Serialize(b, CoachFlowDbContext.Json),
                    v => JsonSerializer.Serialize(v, CoachFlowDbContext.Json).GetHashCode(),
                    v => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(v, CoachFlowDbContext.Json), CoachFlowDbContext.Json)!))
            .IsRequired();
        return property;
    }
}
