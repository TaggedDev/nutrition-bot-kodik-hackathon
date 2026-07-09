using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Nutrition.Core.Domain.Entities;

namespace Nutrition.Infrastructure.Identity;

public sealed class NutritionIdentityDbContext : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>
{
    public NutritionIdentityDbContext(DbContextOptions<NutritionIdentityDbContext> options) : base(options)
    {
    }

    public DbSet<UserMealEntry> UserMealEntries { get; set; }

    public DbSet<UserDailyGoal> UserDailyGoals { get; set; }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<ApplicationUser>(entity =>
        {
            entity.Property(user => user.FirstName).HasMaxLength(100).IsRequired();

            entity.Property(user => user.SecondName).HasMaxLength(100).IsRequired();
        });

        builder.Entity<UserMealEntry>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.UserId).IsRequired();
            entity.Property(e => e.ProductName).HasMaxLength(255).IsRequired();
            entity.Property(e => e.Brand).HasMaxLength(255);
            entity.Property(e => e.Calories).HasPrecision(10, 2);
            entity.Property(e => e.Protein).HasPrecision(10, 2);
            entity.Property(e => e.Fat).HasPrecision(10, 2);
            entity.Property(e => e.Carbs).HasPrecision(10, 2);
            entity.Property(e => e.MealType).IsRequired();
            entity.Property(e => e.ServingGrams).HasPrecision(10, 2);
            entity.Property(e => e.PortionLabel).HasMaxLength(100).IsRequired();
            entity.Property(e => e.SourceType).HasMaxLength(100).IsRequired();
            entity.Property(e => e.SourceReference).HasMaxLength(512).IsRequired();
            entity.Property(e => e.LoggedAtUtc).IsRequired();
            entity.Property(e => e.CreatedAtUtc).IsRequired();
            entity.Property(e => e.UpdatedAtUtc).IsRequired();

            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => new { e.UserId, e.CreatedAtUtc });
            entity.HasIndex(e => new { e.UserId, e.LoggedAtUtc });
        });

        builder.Entity<UserDailyGoal>(entity =>
        {
            entity.HasKey(e => e.UserId);
            entity.Property(e => e.TargetCalories).HasPrecision(10, 2);
            entity.Property(e => e.TargetProtein).HasPrecision(10, 2);
            entity.Property(e => e.TargetFat).HasPrecision(10, 2);
            entity.Property(e => e.TargetCarbs).HasPrecision(10, 2);
            entity.Property(e => e.UpdatedAtUtc).IsRequired();
        });
    }
}
