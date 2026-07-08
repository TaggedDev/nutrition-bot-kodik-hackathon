using Nutrition.Shared.Dtos;

namespace Nutrition.Application.Abstractions.UseCases;

public interface IGetMealNutritionUseCase
{
    Task<GetMealNutritionResponseDto?> ExecuteAsync(GetMealNutritionRequestDto request,
        CancellationToken cancellationToken);
}