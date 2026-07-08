using Nutrition.Shared.Dtos;

namespace Nutrition.Application.Abstractions.Services;

public interface INutritionChatQueryService
{
    Task<NutritionChatSearchResponseDto> SearchAsync(string userInput, CancellationToken cancellationToken);
}
