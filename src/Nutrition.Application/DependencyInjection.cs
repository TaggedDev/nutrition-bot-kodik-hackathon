using System.Net.Http.Headers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Nutrition.Application.Abstractions.Repositories;
using Nutrition.Application.Abstractions.Services;
using Nutrition.Application.Abstractions.UseCases;
using Nutrition.Application.Infrastructure.Mocks;
using Nutrition.Application.Infrastructure.OpenFoodFacts;
using Nutrition.Application.UseCases;

namespace Nutrition.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddNutritionApplication(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<OpenFoodFactsOptions>(configuration.GetSection(OpenFoodFactsOptions.SectionName));
        services.AddMemoryCache();
        services.AddSingleton<IOpenFoodFactsRateLimiter, InMemoryOpenFoodFactsRateLimiter>();

        services.AddSingleton<IMealNutritionRepository, MockMealNutritionRepository>();
        services.AddScoped<IGetMealNutritionUseCase, GetMealNutritionUseCase>();
        services.AddScoped<IUpdateMealNutritionUseCase, UpdateMealNutritionUseCase>();

        services.AddHttpClient<INutritionFactsLookupService, OpenFoodFactsNutritionFactsLookupService>((serviceProvider, client) =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<OpenFoodFactsOptions>>().Value;
            client.BaseAddress = new Uri(options.BaseUrl, UriKind.Absolute);
            client.Timeout = TimeSpan.FromSeconds(options.HttpTimeoutSeconds);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("NutritionPetProject/0.1 (contact: replace-with-real-email)");
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        });

        return services;
    }
}
