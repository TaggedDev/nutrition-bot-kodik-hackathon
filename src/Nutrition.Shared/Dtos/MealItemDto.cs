namespace Nutrition.Shared.Dtos;

public sealed class MealItemDto
{
    public Guid ItemId { get; init; }

    public string ProductName { get; init; } = string.Empty;

    public decimal PortionAmount { get; init; }

    public string PortionUnit { get; init; } = string.Empty;

    public NutritionDto Nutrition { get; init; } = new();

    public decimal ConfidenceScore { get; init; }

    public string SourceType { get; init; } = string.Empty;

    public string SourceReference { get; init; } = string.Empty;
}