namespace Nutrition.Shared.Dtos;

public sealed class ProductNutritionDto
{
    public string ProductId { get; init; } = string.Empty;

    public string ProductName { get; init; } = string.Empty;

    public string? Brand { get; init; }

    public NutritionFactsDto NutritionFacts { get; init; } = new();

    public string SourceType { get; init; } = string.Empty;

    public string SourceReference { get; init; } = string.Empty;

    public decimal ConfidenceScore { get; init; }
}

public sealed class NutritionFactsDto
{
    public decimal Calories { get; init; }

    public decimal Protein { get; init; }

    public decimal Fat { get; init; }

    public decimal Carbs { get; init; }
}