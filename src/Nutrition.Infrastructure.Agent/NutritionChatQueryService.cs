using Nutrition.Application.Abstractions.Services;
using Nutrition.Infrastructure.Agent.NutritionLookup;
using Nutrition.Infrastructure.Agent.WebSearch;
using Microsoft.Extensions.Logging;
using Nutrition.Shared.Dtos;

namespace Nutrition.Infrastructure.Agent;

public sealed class NutritionChatQueryService(IFoodInputParser foodInputParser,
    INutritionFactsLookupService lookupService, IWebSearchService webSearchService, ITavilyQueryBuilder tavilyQueryBuilder,
    INutritionEvidenceExtractor evidenceExtractor, ILogger<NutritionChatQueryService> logger)
    : INutritionChatQueryService
{
    private const int ResultsPerFoodUnit = 3;

    private readonly IFoodInputParser _foodInputParser = foodInputParser;
    private readonly INutritionFactsLookupService _lookupService = lookupService;
    private readonly IWebSearchService _webSearchService = webSearchService;
    private readonly ITavilyQueryBuilder _tavilyQueryBuilder = tavilyQueryBuilder;
    private readonly INutritionEvidenceExtractor _evidenceExtractor = evidenceExtractor;
    private readonly ILogger<NutritionChatQueryService> _logger = logger;

    public async Task<NutritionChatSearchResponseDto> SearchAsync(string userInput, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(userInput)) return new NutritionChatSearchResponseDto { Query = userInput };

        IReadOnlyCollection<FoodUnit> foodUnits = await _foodInputParser.ParseAsync(userInput, cancellationToken);

        if (foodUnits.Count == 0) return new NutritionChatSearchResponseDto { Query = userInput.Trim() };

        var clarifications = new List<NutritionClarificationDto>();

        foreach (var foodUnit in foodUnits.Where(unit => !string.IsNullOrWhiteSpace(unit.ProductName))
                     .DistinctBy(unit => unit.ProductName.Trim(), StringComparer.OrdinalIgnoreCase))
        {
            var matches = await FindNutritionCandidatesAsync(foodUnit, cancellationToken);
            if (matches.Count == 0) continue;

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
            Query = userInput.Trim(), Items = Array.Empty<ProductNutritionDto>(), Clarifications = clarifications,
        };
    }

    private async Task<IReadOnlyCollection<ProductNutritionDto>> FindNutritionCandidatesAsync(FoodUnit foodUnit,
        CancellationToken cancellationToken)
    {
        if (foodUnit.Kind == FoodUnitKind.MassMarketProduct)
        {
            var openFoodFactsCandidates = await OpenFoodFactsCandidates(foodUnit, cancellationToken);
            if (openFoodFactsCandidates.Count > 0)
            {
                return openFoodFactsCandidates;
            }

            _logger.LogInformation(
                "Nutrition lookup OFF returned no candidates for '{ProductName}'; falling back to web search",
                foodUnit.ProductName);
        }

        return await GetWebSearchCandidates(foodUnit, cancellationToken);
    }

    private async Task<IReadOnlyCollection<ProductNutritionDto>> OpenFoodFactsCandidates(FoodUnit foodUnit,
        CancellationToken cancellationToken)
    {
        var openFoodFactsCandidates = await _lookupService.SearchAsync(BuildOpenFoodFactsQuery(foodUnit),
            cancellationToken);
        _logger.LogInformation(
            "Nutrition lookup OFF returned {Count} candidates for product '{ProductName}', brand '{Brand}', kind {Kind}",
            openFoodFactsCandidates.Count, foodUnit.ProductName, foodUnit.Brand, foodUnit.Kind);
        return openFoodFactsCandidates.Take(ResultsPerFoodUnit).ToArray();
    }

    private async Task<IReadOnlyCollection<ProductNutritionDto>> GetWebSearchCandidates(FoodUnit foodUnit,
        CancellationToken cancellationToken)
    {
        string tavilyQuery = _tavilyQueryBuilder.Build(foodUnit);
        var webSearch = await _webSearchService.SearchAsync(
            new WebSearchRequest(tavilyQuery, MaxResults: 5, Depth: WebSearchDepth.Advanced), cancellationToken);
        _logger.LogInformation("Nutrition lookup Tavily returned {Count} sources for query: {Query}",
            webSearch.Results.Count, tavilyQuery);
        var extracted = await _evidenceExtractor.ExtractAsync(foodUnit, webSearch.Results, cancellationToken);
        _logger.LogInformation("Nutrition lookup extractor returned {Count} candidates for '{ProductName}'",
            extracted.Count, foodUnit.ProductName);
        return extracted;
    }

    private static string BuildClarificationQuestion(string productName)
        => $"Выберите подходящий вариант для \"{productName}\"";

    private static string BuildOpenFoodFactsQuery(FoodUnit foodUnit)
        => string.IsNullOrWhiteSpace(foodUnit.Brand) ? foodUnit.ProductName.Trim()
            : $"{foodUnit.Brand.Trim()} {foodUnit.ProductName.Trim()}";
}
