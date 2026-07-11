using Nutrition.Application.Abstractions.Services;
using Nutrition.Infrastructure.Agent.NutritionLookup;
using Nutrition.Infrastructure.Agent.WebSearch;
using Microsoft.Extensions.Logging;
using Nutrition.Shared.Dtos;

namespace Nutrition.Infrastructure.Agent;

  public sealed class NutritionChatQueryService(IFoodInputParser foodInputParser,
    INutritionFactsLookupService lookupService, IOpenFoodFactsCandidateJudge candidateJudge,
    IWebSearchService webSearchService, ITavilyQueryBuilder tavilyQueryBuilder,
    INutritionEvidenceExtractor evidenceExtractor, ILogger<NutritionChatQueryService> logger)
    : INutritionChatQueryService
{
    private const int ResultsPerFoodUnit = 3;

    private readonly IFoodInputParser _foodInputParser = foodInputParser;
    private readonly INutritionFactsLookupService _lookupService = lookupService;
    private readonly IOpenFoodFactsCandidateJudge _candidateJudge = candidateJudge;
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
        bool serviceUnavailable = false;

        foreach (var foodUnit in foodUnits)
        {
            var matches = await FindNutritionCandidatesAsync(foodUnit, cancellationToken);
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

    private async Task<IReadOnlyCollection<ProductNutritionDto>> FindNutritionCandidatesAsync(FoodUnit foodUnit,
        CancellationToken cancellationToken)
    {
        switch (foodUnit.Kind)
        {
            case FoodUnitKind.MassMarketProduct:
                {
                    IReadOnlyCollection<ProductNutritionDto> openFoodFactsCandidates =
                        await OpenFoodFactsCandidates(foodUnit, cancellationToken);
                    if (openFoodFactsCandidates.Count != 0)
                        return openFoodFactsCandidates.Take(ResultsPerFoodUnit).ToArray();

                    IReadOnlyCollection<ProductNutritionDto> extracted =
                        await GetWebSearchCandidates(foodUnit, cancellationToken);
                    return extracted;
                }
            case FoodUnitKind.PreparedFood:
                {
                    IReadOnlyCollection<ProductNutritionDto> extracted =
                        await GetWebSearchCandidates(foodUnit, cancellationToken);
                    return extracted;
                }
            case FoodUnitKind.Unknown:
            default:
                return new List<ProductNutritionDto>();
        }

        async Task<IReadOnlyCollection<ProductNutritionDto>> OpenFoodFactsCandidates(FoodUnit foodUnit1,
            CancellationToken cancellationToken1)
        {
            var openFoodFactsCandidates = await _lookupService.SearchAsync(BuildOpenFoodFactsQuery(foodUnit1),
                cancellationToken1);
            _logger.LogInformation(
                "Nutrition lookup OFF returned {Count} candidates for product '{ProductName}', brand '{Brand}', kind {Kind}",
                openFoodFactsCandidates.Count, foodUnit1.ProductName, foodUnit1.Brand, foodUnit1.Kind);
            return await _candidateJudge.SelectAcceptableAsync(foodUnit, openFoodFactsCandidates, cancellationToken);
        }

        async Task<IReadOnlyCollection<ProductNutritionDto>> GetWebSearchCandidates(FoodUnit foodUnit2,
            CancellationToken cancellationToken2)
        {
            string tavilyQuery = _tavilyQueryBuilder.Build(foodUnit2);
            var webSearch = await _webSearchService.SearchAsync(
                new WebSearchRequest(tavilyQuery, MaxResults: 5, Depth: WebSearchDepth.Advanced), cancellationToken2);
            _logger.LogInformation("Nutrition lookup Tavily returned {Count} sources for query: {Query}",
                webSearch.Results.Count, tavilyQuery);
            var extracted = await _evidenceExtractor.ExtractAsync(foodUnit2, webSearch.Results, cancellationToken2);
            _logger.LogInformation("Nutrition lookup extractor returned {Count} candidates for '{ProductName}'",
                extracted.Count, foodUnit2.ProductName);
            return extracted;
        }
    }

    private static string BuildClarificationQuestion(string productName)
        => $"Выберите подходящий вариант для \"{productName}\"";

    private static string BuildOpenFoodFactsQuery(FoodUnit foodUnit)
        => string.IsNullOrWhiteSpace(foodUnit.Brand) ? foodUnit.ProductName.Trim()
            : $"{foodUnit.Brand.Trim()} {foodUnit.ProductName.Trim()}";
}