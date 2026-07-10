using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nutrition.Application.Abstractions.Repositories;
using Nutrition.Application.Abstractions.Services;

namespace Nutrition.Infrastructure.Identity;

public static class DependencyInjection
{
    public static IServiceCollection AddNutritionIdentity(this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("IdentityDb");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("Connection string 'IdentityDb' is not configured.");
        }

        services.AddDbContext<NutritionIdentityDbContext>(options => options.UseNpgsql(connectionString));

        services.AddIdentity<ApplicationUser, IdentityRole<Guid>>(options =>
        {
            options.User.RequireUniqueEmail = true;
            options.SignIn.RequireConfirmedAccount = false;
            options.Password.RequiredLength = 8;
            options.Password.RequireDigit = true;
            options.Password.RequireLowercase = true;
            options.Password.RequireUppercase = false;
            options.Password.RequireNonAlphanumeric = false;
        }).AddEntityFrameworkStores<NutritionIdentityDbContext>().AddDefaultTokenProviders();

        services.AddScoped<IUserMealEntryRepository, UserMealEntryRepository>();
        services.AddScoped<IUserDailyGoalRepository, UserDailyGoalRepository>();
        services.AddScoped<IProfileService, ProfileService>();

        return services;
    }
}
