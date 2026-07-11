namespace Nutrition.Shared.Dtos;

public sealed class NutritionChatSearchResponseDto
{
    public string Query { get; init; } = string.Empty;

    public IReadOnlyCollection<ProductNutritionDto> Items { get; init; } = Array.Empty<ProductNutritionDto>();

    public IReadOnlyCollection<NutritionClarificationDto> Clarifications { get; init; } =
        Array.Empty<NutritionClarificationDto>();

    public bool RequiresClarification => Clarifications.Count > 0;

    public bool ServiceUnavailable { get; init; }
}

public sealed class NutritionClarificationDto
{
    public string Id { get; init; } = string.Empty;

    public string OriginalInput { get; init; } = string.Empty;

    public string ParsedProductName { get; init; } = string.Empty;

    public string Question { get; init; } = string.Empty;

    public IReadOnlyCollection<ProductNutritionDto> Candidates { get; init; } = Array.Empty<ProductNutritionDto>();
}
