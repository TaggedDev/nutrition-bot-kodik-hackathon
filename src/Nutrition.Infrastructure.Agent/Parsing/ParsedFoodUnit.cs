namespace Nutrition.Infrastructure.Agent.Parsing;

public sealed class ParsedFoodUnit
{
    public string ProductName { get; init; } = string.Empty;

    public decimal Quantity { get; init; } = 1;

    public string Unit { get; init; } = "serving";

    public string? Brand { get; init; }

    public string? Preparation { get; init; }

    public FoodUnitKind Kind { get; init; } = FoodUnitKind.Unknown;
}