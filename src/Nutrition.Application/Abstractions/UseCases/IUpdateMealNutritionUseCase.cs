using Nutrition.Shared.Dtos;

namespace Nutrition.Application.Abstractions.UseCases;

public interface IUpdateMealNutritionUseCase
{
    Task<UpdateMealNutritionResponseDto?> ExecuteAsync(UpdateMealNutritionRequestDto request,
        CancellationToken cancellationToken);
}