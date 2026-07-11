namespace Nutrition.Shared.Dtos;

public sealed class NutritionChatSearchResponseDto
{
    public string Query { get; init; } = string.Empty;

    public IReadOnlyCollection<ProductNutritionDto> Items { get; init; } = Array.Empty<ProductNutritionDto>();

    public IReadOnlyCollection<NutritionClarificationDto> Clarifications { get; init; } =
        Array.Empty<NutritionClarificationDto>();

    public bool RequiresClarification => Clarifications.Count > 0;

}
