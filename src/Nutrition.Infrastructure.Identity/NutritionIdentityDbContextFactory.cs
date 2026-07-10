using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Nutrition.Infrastructure.Identity;

public sealed class NutritionIdentityDbContextFactory : IDesignTimeDbContextFactory<NutritionIdentityDbContext>
{
    public NutritionIdentityDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<NutritionIdentityDbContext>();
        optionsBuilder.UseNpgsql("Host=localhost;Port=5432;Database=nutrition;Username=postgres;Password=postgres");
        return new NutritionIdentityDbContext(optionsBuilder.Options);
    }
}
