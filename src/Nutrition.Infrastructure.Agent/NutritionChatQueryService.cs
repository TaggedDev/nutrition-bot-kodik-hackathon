using Nutrition.Application.Abstractions.Services;
using Nutrition.Shared.Dtos;

namespace Nutrition.Infrastructure.Agent;

public sealed class NutritionChatQueryService : INutritionChatQueryService
{
    private const int ResultsPerFoodUnit = 3;

    private readonly IFoodInputParser _foodInputParser;
    private readonly INutritionFactsLookupService _lookupService;

    public NutritionChatQueryService(IFoodInputParser foodInputParser, INutritionFactsLookupService lookupService)
    {
        _foodInputParser = foodInputParser;
        _lookupService = lookupService;
    }

    public async Task<NutritionChatSearchResponseDto> SearchAsync(string userInput, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(userInput))
        {
            return new NutritionChatSearchResponseDto { Query = userInput };
        }

        var foodUnits = await _foodInputParser.ParseAsync(userInput, cancellationToken);
        var normalizedUnits = foodUnits.Where(unit => !string.IsNullOrWhiteSpace(unit.ProductName))
            .DistinctBy(unit => unit.ProductName.Trim(), StringComparer.OrdinalIgnoreCase).ToArray();

        if (normalizedUnits.Length == 0)
        {
            return new NutritionChatSearchResponseDto { Query = userInput.Trim() };
        }

        var clarifications = new List<NutritionClarificationDto>();

        foreach (var foodUnit in normalizedUnits)
        {
            var matches = await _lookupService.SearchAsync(foodUnit.ProductName, cancellationToken);

            clarifications.Add(new NutritionClarificationDto
            {
                Id = Guid.NewGuid().ToString("N"),
                OriginalInput = userInput.Trim(),
                ParsedProductName = foodUnit.ProductName,
                Question = BuildClarificationQuestion(foodUnit.ProductName),
                Candidates = matches.Take(ResultsPerFoodUnit).ToArray()
            });
        }

        return new NutritionChatSearchResponseDto
        {
            Query = userInput.Trim(), Items = Array.Empty<ProductNutritionDto>(), Clarifications = clarifications
        };
    }

    private static string BuildClarificationQuestion(string productName)
    {
        return $"Выберите подходящий вариант для \"{productName}\"";
    }
}