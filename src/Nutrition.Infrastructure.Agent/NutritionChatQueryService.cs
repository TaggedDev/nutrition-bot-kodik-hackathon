using Nutrition.Application.Abstractions.Services;
using Nutrition.Shared.Dtos;

namespace Nutrition.Infrastructure.Agent;

public sealed class NutritionChatQueryService : INutritionChatQueryService
{
    private const int ResultsPerFoodUnit = 3;

    private readonly IFoodInputParser _foodInputParser;
    private readonly INutritionFactsLookupService _lookupService;

    public NutritionChatQueryService(
        IFoodInputParser foodInputParser,
        INutritionFactsLookupService lookupService)
    {
        _foodInputParser = foodInputParser;
        _lookupService = lookupService;
    }

    public async Task<IReadOnlyCollection<ProductNutritionDto>> SearchAsync(
        string userInput,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(userInput))
        {
            return Array.Empty<ProductNutritionDto>();
        }

        var foodUnits = await _foodInputParser.ParseAsync(userInput, cancellationToken);
        var normalizedUnits = foodUnits
            .Where(unit => !string.IsNullOrWhiteSpace(unit.ProductName))
            .DistinctBy(unit => unit.ProductName.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (normalizedUnits.Length == 0)
        {
            return Array.Empty<ProductNutritionDto>();
        }

        var results = new List<ProductNutritionDto>();

        foreach (var foodUnit in normalizedUnits)
        {
            var matches = await _lookupService.SearchAsync(foodUnit.ProductName, cancellationToken);
            results.AddRange(matches.Take(ResultsPerFoodUnit));
        }

        return results;
    }
}
