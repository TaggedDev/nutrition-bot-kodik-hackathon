using Nutrition.Shared.Dtos;

namespace Nutrition.Infrastructure.Agent.NutritionLookup;

public interface IOpenFoodFactsCandidateJudge
{
    Task<IReadOnlyCollection<ProductNutritionDto>> SelectAcceptableAsync(FoodUnit foodUnit,
        IReadOnlyCollection<ProductNutritionDto> candidates, CancellationToken cancellationToken);
}