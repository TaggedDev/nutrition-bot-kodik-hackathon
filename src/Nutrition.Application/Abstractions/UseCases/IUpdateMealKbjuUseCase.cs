using Nutrition.Shared.Dtos;

namespace Nutrition.Application.Abstractions.UseCases;

public interface IUpdateMealKbjuUseCase
{
    Task<UpdateMealKbjuResponseDto?> ExecuteAsync(UpdateMealKbjuRequestDto request, CancellationToken cancellationToken);
}
