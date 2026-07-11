namespace Nutrition.Infrastructure.Agent.NutritionLookup;

public interface ITavilyQueryBuilder
{
    string Build(FoodUnit foodUnit);
}