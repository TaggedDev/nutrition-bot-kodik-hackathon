namespace Nutrition.Shared.Dtos;

public sealed class NutritionClarificationDto
{
    public string Id { get; init; } = string.Empty;

    public string OriginalInput { get; init; } = string.Empty;

    public string ParsedProductName { get; init; } = string.Empty;

    public string Question { get; init; } = string.Empty;

    public IReadOnlyCollection<ProductNutritionDto> Candidates { get; init; } = Array.Empty<ProductNutritionDto>();
}