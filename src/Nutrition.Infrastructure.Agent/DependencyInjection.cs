using Microsoft.Extensions.DependencyInjection;
using Nutrition.Infrastructure.Agent.Matching;
using Nutrition.Infrastructure.Agent.Parsing;

namespace Nutrition.Infrastructure.Agent;

/// <summary>
/// Регистрация зависимостей для слоя Agent.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddNutritionAgent(this IServiceCollection services)
    {
        // TODO: Регистрировать парсер после настройки MAF
        // services.AddScoped<IFoodInputParser, MafFoodInputParser>();

        services.AddScoped<IFoodMatcher, OpenFoodFactsFoodMatcher>();

        return services;
    }
}
