using Nutrition.Shared.Dtos;

namespace Nutrition.Application.Abstractions.Services;

public interface INutritionFactsLookupService
{
    Task<IReadOnlyCollection<ProductNutritionDto>> SearchAsync(string query, CancellationToken cancellationToken);
}