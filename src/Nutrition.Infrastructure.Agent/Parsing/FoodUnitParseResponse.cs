namespace Nutrition.Infrastructure.Agent.Parsing;

public sealed class FoodUnitParseResponse
{
    public IReadOnlyCollection<ParsedFoodUnit> Items { get; init; } = Array.Empty<ParsedFoodUnit>();
}