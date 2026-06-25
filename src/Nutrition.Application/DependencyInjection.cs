using Microsoft.Extensions.DependencyInjection;
using Nutrition.Application.Abstractions.Repositories;
using Nutrition.Application.Abstractions.Services;
using Nutrition.Application.Abstractions.UseCases;
using Nutrition.Application.Infrastructure.Mocks;
using Nutrition.Application.Infrastructure.OpenFoodFacts;
using Nutrition.Application.UseCases;

namespace Nutrition.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddNutritionApplication(this IServiceCollection services)
    {
        services.AddSingleton<IMealKbjuRepository, MockMealKbjuRepository>();
        services.AddScoped<IGetMealKbjuUseCase, GetMealKbjuUseCase>();
        services.AddScoped<IUpdateMealKbjuUseCase, UpdateMealKbjuUseCase>();
        services.AddHttpClient<INutritionFactsLookupService, OpenFoodFactsNutritionFactsLookupService>(client =>
        {
            client.BaseAddress = new Uri("https://world.openfoodfacts.org");
            client.Timeout = TimeSpan.FromSeconds(10);
        });

        return services;
    }
}
