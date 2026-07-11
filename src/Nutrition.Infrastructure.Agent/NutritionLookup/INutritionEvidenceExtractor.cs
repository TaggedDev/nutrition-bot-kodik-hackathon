using Nutrition.Infrastructure.Agent.WebSearch;
using Nutrition.Shared.Dtos;

namespace Nutrition.Infrastructure.Agent.NutritionLookup;

public interface INutritionEvidenceExtractor
{
    Task<IReadOnlyCollection<ProductNutritionDto>> ExtractAsync(FoodUnit foodUnit,
        IReadOnlyCollection<WebSearchResult> sources, CancellationToken cancellationToken);
}