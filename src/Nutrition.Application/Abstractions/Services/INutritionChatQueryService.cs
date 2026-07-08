using Nutrition.Shared.Dtos;

namespace Nutrition.Application.Abstractions.Services;

public interface INutritionChatQueryService
{
    Task<IReadOnlyCollection<ProductNutritionDto>> SearchAsync(string userInput, CancellationToken cancellationToken);
}
