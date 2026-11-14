using CoachFlow.Api.Accounts;
using CoachFlow.Api.Exercises;
using CoachFlow.Api.Plans;
using CoachFlow.Api.Suggestions;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace CoachFlow.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options)
    : IdentityUserContext<AppUser>(options), IDataProtectionKeyContext
{
    public DbSet<Invitation> Invitations => Set<Invitation>();

    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

    public DbSet<Focus> Focuses => Set<Focus>();

    public DbSet<Exercise> Exercises => Set<Exercise>();

    public DbSet<TrainingPlan> TrainingPlans => Set<TrainingPlan>();

    public DbSet<AiUsage> AiUsages => Set<AiUsage>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // SQLite speichert keine Zeitzone. Alle Zeitpunkte sind UTC und werden beim Lesen auch so markiert,
        // sonst erschienen sie im JSON ohne "Z" und der Browser läse sie als Ortszeit.
        configurationBuilder.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();
        configurationBuilder.Properties<DateTime?>().HaveConversion<UtcDateTimeConverter>();
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<AppUser>(user =>
        {
            user.Property(u => u.Name).HasMaxLength(100);
        });

        builder.Entity<Invitation>(invitation =>
        {
            invitation.Property(i => i.Email).HasMaxLength(256);
            invitation.Property(i => i.TokenHash).HasMaxLength(64);
            invitation.HasIndex(i => i.TokenHash).IsUnique();
            invitation.HasIndex(i => i.Email);
            invitation.HasOne(i => i.InvitedBy)
                .WithMany()
                .HasForeignKey(i => i.InvitedById)
                .OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<Focus>(focus =>
        {
            focus.Property(f => f.Sport).HasMaxLength(32);
            focus.Property(f => f.Name).HasMaxLength(ExerciseEndpoints.MaxFocusNameLength);
            focus.Property(f => f.NormalizedName).HasMaxLength(ExerciseEndpoints.MaxFocusNameLength);
            focus.HasIndex(f => new { f.Sport, f.NormalizedName }).IsUnique();
            focus.HasOne(f => f.CreatedBy)
                .WithMany()
                .HasForeignKey(f => f.CreatedById)
                .OnDelete(DeleteBehavior.SetNull);
            focus.HasData(FocusCatalog.Initial);
        });

        builder.Entity<Exercise>(exercise =>
        {
            exercise.Property(e => e.Sport).HasMaxLength(32);
            exercise.Property(e => e.Name).HasMaxLength(ExerciseEndpoints.MaxNameLength);
            exercise.Property(e => e.Description).HasMaxLength(ExerciseEndpoints.MaxDescriptionLength);
            exercise.Property(e => e.Level).HasConversion<string>().HasMaxLength(32);
            exercise.Property(e => e.Origin).HasConversion<string>().HasMaxLength(16);
            exercise.Property(e => e.Version).IsConcurrencyToken();
            exercise.HasIndex(e => e.Sport);
            exercise.HasMany(e => e.Focuses)
                .WithMany()
                .UsingEntity("ExerciseFocuses");
            exercise.HasOne(e => e.UpdatedBy)
                .WithMany()
                .HasForeignKey(e => e.UpdatedById)
                .OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<TrainingPlan>(plan =>
        {
            plan.Property(p => p.Sport).HasMaxLength(32);
            plan.Property(p => p.Title).HasMaxLength(PlanEndpoints.MaxTitleLength);
            plan.Property(p => p.Note).HasMaxLength(PlanEndpoints.MaxNoteLength);
            plan.Property(p => p.Level).HasConversion<string>().HasMaxLength(32);
            plan.Property(p => p.Spread).HasConversion<string>().HasMaxLength(16);
            plan.Property(p => p.Version).IsConcurrencyToken();
            plan.HasIndex(p => p.Sport);
            plan.HasMany(p => p.Focuses)
                .WithMany()
                .UsingEntity("PlanFocuses");
            plan.HasMany(p => p.Positions)
                .WithOne()
                .HasForeignKey(p => p.PlanId)
                .OnDelete(DeleteBehavior.Cascade);
            plan.HasOne(p => p.UpdatedBy)
                .WithMany()
                .HasForeignKey(p => p.UpdatedById)
                .OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<PlanPosition>(position =>
        {
            position.Property(p => p.Name).HasMaxLength(ExerciseEndpoints.MaxNameLength);
            position.Property(p => p.Description).HasMaxLength(ExerciseEndpoints.MaxDescriptionLength);
            position.Property(p => p.Note).HasMaxLength(PlanEndpoints.MaxPositionNoteLength);
            // Gelöschte Übungen lassen die Position samt Kopie bestehen, nur der Verweis wird leer.
            position.HasOne<Exercise>()
                .WithMany()
                .HasForeignKey(p => p.ExerciseId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<AiUsage>(usage =>
        {
            usage.Property(u => u.Provider).HasMaxLength(32);
            usage.Property(u => u.Result).HasConversion<string>().HasMaxLength(32);
            usage.Property(u => u.EstimatedCostEuro).HasPrecision(12, 6);
            usage.HasIndex(u => u.CreatedAt);
            usage.HasOne(u => u.User)
                .WithMany()
                .HasForeignKey(u => u.UserId)
                .OnDelete(DeleteBehavior.SetNull);
        });
    }

    private sealed class UtcDateTimeConverter() : ValueConverter<DateTime, DateTime>(
        value => value.ToUniversalTime(),
        value => DateTime.SpecifyKind(value, DateTimeKind.Utc));
}
