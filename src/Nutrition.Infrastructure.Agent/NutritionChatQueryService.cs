using Nutrition.Application.Abstractions.Services;
using Nutrition.Infrastructure.Agent.NutritionLookup;
using Nutrition.Infrastructure.Agent.WebSearch;
using Microsoft.Extensions.Logging;
using Nutrition.Shared.Dtos;

namespace Nutrition.Infrastructure.Agent;

public sealed class NutritionChatQueryService : INutritionChatQueryService
{
    private const int ResultsPerFoodUnit = 3;

    private readonly IFoodInputParser _foodInputParser;
    private readonly INutritionFactsLookupService _lookupService;
    private readonly IOpenFoodFactsCandidateJudge _candidateJudge;
    private readonly IWebSearchService _webSearchService;
    private readonly ITavilyQueryBuilder _tavilyQueryBuilder;
    private readonly INutritionEvidenceExtractor _evidenceExtractor;
    private readonly ILogger<NutritionChatQueryService> _logger;

    public NutritionChatQueryService(
        IFoodInputParser foodInputParser,
        INutritionFactsLookupService lookupService,
        IOpenFoodFactsCandidateJudge candidateJudge,
        IWebSearchService webSearchService,
        ITavilyQueryBuilder tavilyQueryBuilder,
        INutritionEvidenceExtractor evidenceExtractor,
        ILogger<NutritionChatQueryService> logger)
    {
        _foodInputParser = foodInputParser;
        _lookupService = lookupService;
        _candidateJudge = candidateJudge;
        _webSearchService = webSearchService;
        _tavilyQueryBuilder = tavilyQueryBuilder;
        _evidenceExtractor = evidenceExtractor;
        _logger = logger;
    }

    public async Task<NutritionChatSearchResponseDto> SearchAsync(string userInput, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(userInput))
        {
            return new NutritionChatSearchResponseDto { Query = userInput };
        }

        var foodUnits = await _foodInputParser.ParseAsync(userInput, cancellationToken);
        var normalizedUnits = foodUnits.Where(unit => !string.IsNullOrWhiteSpace(unit.ProductName))
            .Select(FoodUnitNormalizer.Normalize)
            .DistinctBy(BuildDeduplicationKey, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (normalizedUnits.Length == 0)
        {
            return new NutritionChatSearchResponseDto { Query = userInput.Trim() };
        }

        var clarifications = new List<NutritionClarificationDto>();
        var serviceUnavailable = false;

        foreach (var foodUnit in normalizedUnits)
        {
            var matches = await FindCandidatesAsync(foodUnit, cancellationToken);
            if (matches.Count == 0)
            {
                serviceUnavailable = true;
                break;
            }

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
            Query = userInput.Trim(),
            Items = Array.Empty<ProductNutritionDto>(),
            Clarifications = serviceUnavailable ? Array.Empty<NutritionClarificationDto>() : clarifications,
            ServiceUnavailable = serviceUnavailable
        };
    }

    private async Task<IReadOnlyCollection<ProductNutritionDto>> FindCandidatesAsync(
        FoodUnit foodUnit,
        CancellationToken cancellationToken)
    {
        var openFoodFactsCandidates = await _lookupService.SearchAsync(BuildOpenFoodFactsQuery(foodUnit),
            cancellationToken);
        _logger.LogInformation(
            "Nutrition lookup OFF returned {Count} candidates for product '{ProductName}', brand '{Brand}', kind {Kind}",
            openFoodFactsCandidates.Count,
            foodUnit.ProductName,
            foodUnit.Brand,
            foodUnit.Kind);

        if (foodUnit.Kind == FoodUnitKind.MassMarketProduct && openFoodFactsCandidates.Count > 0)
        {
            return openFoodFactsCandidates.Take(ResultsPerFoodUnit).ToArray();
        }

        var acceptedOpenFoodFacts =
            await _candidateJudge.SelectAcceptableAsync(foodUnit, openFoodFactsCandidates, cancellationToken);

        if (acceptedOpenFoodFacts.Count > 0)
        {
            _logger.LogInformation("Nutrition lookup OFF judge accepted {Count} candidates for '{ProductName}'",
                acceptedOpenFoodFacts.Count,
                foodUnit.ProductName);
            return acceptedOpenFoodFacts.Take(ResultsPerFoodUnit).ToArray();
        }

        var tavilyQuery = _tavilyQueryBuilder.Build(foodUnit);
        var webSearch = await _webSearchService.SearchAsync(
            new WebSearchRequest(tavilyQuery, MaxResults: 5, Depth: WebSearchDepth.Advanced),
            cancellationToken);
        _logger.LogInformation("Nutrition lookup Tavily returned {Count} sources for query: {Query}",
            webSearch.Results.Count,
            tavilyQuery);

        var extracted = await _evidenceExtractor.ExtractAsync(foodUnit, webSearch.Results, cancellationToken);
        _logger.LogInformation("Nutrition lookup extractor returned {Count} candidates for '{ProductName}'",
            extracted.Count,
            foodUnit.ProductName);

        return extracted;
    }

    private static string BuildClarificationQuestion(string productName)
        => $"Выберите подходящий вариант для \"{productName}\"";

    private static string BuildOpenFoodFactsQuery(FoodUnit foodUnit)
    {
        if (string.IsNullOrWhiteSpace(foodUnit.Brand))
        {
            return foodUnit.ProductName.Trim();
        }

        return $"{foodUnit.Brand.Trim()} {foodUnit.ProductName.Trim()}";
    }

    private static string BuildDeduplicationKey(FoodUnit foodUnit)
        => string.Join('|',
            foodUnit.ProductName.Trim(),
            foodUnit.Brand?.Trim() ?? string.Empty,
            foodUnit.Preparation?.Trim() ?? string.Empty,
            foodUnit.Kind.ToString());
}
