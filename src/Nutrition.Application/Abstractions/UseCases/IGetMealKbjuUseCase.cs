using Nutrition.Shared.Dtos;

namespace Nutrition.Application.Abstractions.UseCases;

public interface IGetMealKbjuUseCase
{
    Task<GetMealKbjuResponseDto?> ExecuteAsync(GetMealKbjuRequestDto request, CancellationToken cancellationToken);
}
