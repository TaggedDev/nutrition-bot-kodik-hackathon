using Nutrition.Infrastructure.Agent;
using Nutrition.Infrastructure.Agent.NutritionLookup;
using Nutrition.Infrastructure.Agent.WebSearch;
using Microsoft.Extensions.Logging.Abstractions;
using Nutrition.Shared.Dtos;

namespace Nutrition.Application.Tests;

public sealed class NutritionChatQueryServiceTests
{
    [Fact]
    public async Task SearchAsync_ReturnsTrimmedQuery_WhenParserReturnsNoUnits()
    {
        var parser = new FakeFoodInputParser(Array.Empty<FoodUnit>());
        var lookup = new FakeNutritionFactsLookupService();
        var service = CreateService(parser, lookup);

        var result = await service.SearchAsync("   pasta   ", CancellationToken.None);

        Assert.Equal("pasta", result.Query);
        Assert.Empty(result.Items);
        Assert.Empty(result.Clarifications);
        Assert.Empty(lookup.Queries);
    }

    [Fact]
    public async Task SearchAsync_DeduplicatesUnits_AndLimitsCandidatesToTopThree()
    {
        var parser = new FakeFoodInputParser(new[]
        {
            new FoodUnit
            {
                ProductName = "pasta", Quantity = 1, Unit = "serving", Kind = FoodUnitKind.MassMarketProduct
            },
            new FoodUnit
            {
                ProductName = " pasta ", Quantity = 2, Unit = "serving", Kind = FoodUnitKind.MassMarketProduct
            },
            new FoodUnit
            {
                ProductName = "chicken", Quantity = 1, Unit = "serving", Kind = FoodUnitKind.MassMarketProduct
            },
            new FoodUnit
            {
                ProductName = " ", Quantity = 1, Unit = "serving", Kind = FoodUnitKind.MassMarketProduct
            }
        });
        var lookup = new FakeNutritionFactsLookupService();
        var service = CreateService(parser, lookup);

        var result = await service.SearchAsync("pasta with chicken", CancellationToken.None);

        Assert.Equal(new[] { "pasta", "chicken" }, lookup.Queries);
        Assert.True(result.RequiresClarification);
        Assert.Empty(result.Items);
        Assert.Equal(2, result.Clarifications.Count);
        Assert.All(result.Clarifications, clarification => Assert.Equal(3, clarification.Candidates.Count));
    }

    [Fact]
    public async Task SearchAsync_MassMarketProduct_UsesBrandInOpenFoodFactsQuery_AndDoesNotSearchWeb()
    {
        var parser = new FakeFoodInputParser(new[]
        {
            new FoodUnit
            {
                ProductName = "макароны вермишель",
                Brand = "макфа",
                Quantity = 1,
                Unit = "serving",
                Kind = FoodUnitKind.MassMarketProduct
            }
        });
        var lookup = new FakeNutritionFactsLookupService();
        var webSearch = new FakeWebSearchService();
        var service = CreateService(parser, lookup, webSearch: webSearch);

        await service.SearchAsync("макароны вермишель макфа", CancellationToken.None);

        Assert.Equal(new[] { "макфа макароны вермишель" }, lookup.Queries);
        Assert.Empty(webSearch.Queries);
    }

    [Fact]
    public async Task SearchAsync_PreparedFood_UsesWebSearchWithoutOpenFoodFacts()
    {
        var parser = new FakeFoodInputParser(new[]
        {
            new FoodUnit
            {
                ProductName = "coffee latte",
                Brand = "cofix",
                Quantity = 1,
                Unit = "serving",
                Kind = FoodUnitKind.PreparedFood
            }
        });
        var lookup = new FakeNutritionFactsLookupService();
        var webSearch = new FakeWebSearchService();
        var extractor = new FakeEvidenceExtractor
        {
            Candidates = new[]
            {
                new ProductNutritionDto
                {
                    ProductId = "web-latte",
                    ProductName = "latte",
                    SourceType = "WebSearch",
                    SourceReference = "https://example.test/latte",
                    ConfidenceScore = 1
                }
            }
        };
        var service = CreateService(parser, lookup, webSearch: webSearch, extractor: extractor);

        var result = await service.SearchAsync("coffee latte cofix", CancellationToken.None);

        Assert.Empty(lookup.Queries);
        Assert.Single(webSearch.Queries);
        Assert.Equal("web-latte", result.Clarifications.Single().Candidates.Single().ProductId);
    }

    [Fact]
    public async Task SearchAsync_PreparedFood_SearchesWebAndReturnsExtractedCandidates()
    {
        var parser = new FakeFoodInputParser(new[]
        {
            new FoodUnit
            {
                ProductName = "gazpacho",
                Brand = "creative kitchen",
                Quantity = 1,
                Unit = "serving",
                Kind = FoodUnitKind.PreparedFood
            }
        });
        var lookup = new FakeNutritionFactsLookupService();
        var webSearch = new FakeWebSearchService
        {
            Results = new[]
            {
                new WebSearchResult("Gazpacho", new Uri("https://example.com/gazpacho"), "nutrition", 0.9m)
            }
        };
        var extractor = new FakeEvidenceExtractor
        {
            Candidates = new[]
            {
                new ProductNutritionDto
                {
                    ProductId = "web",
                    ProductName = "gazpacho",
                    Brand = "creative kitchen",
                    NutritionValueBasis = "PerServing",
                    ServingSize = 320,
                    ServingUnit = "g",
                    SourceType = "WebSearch",
                    SourceReference = "https://example.com/gazpacho",
                    ConfidenceScore = 0.9m
                }
            }
        };
        var service = CreateService(parser, lookup, webSearch: webSearch, extractor: extractor);

        var result = await service.SearchAsync("gazpacho creative kitchen", CancellationToken.None);

        Assert.Single(webSearch.Queries);
        Assert.Contains("creative kitchen", webSearch.Queries.Single(), StringComparison.Ordinal);
        Assert.Contains("gazpacho", webSearch.Queries.Single(), StringComparison.Ordinal);
        Assert.Equal("web", result.Clarifications.Single().Candidates.Single().ProductId);
    }

    [Fact]
    public async Task SearchAsync_UnknownFoodUnit_UsesWebSearchWithoutOpenFoodFacts()
    {
        var parser = new FakeFoodInputParser(new[]
        {
            new FoodUnit
            {
                ProductName = "tanuki arigato set",
                Quantity = 0.5m,
                Unit = "serving",
                Kind = FoodUnitKind.Unknown
            }
        });
        var lookup = new FakeNutritionFactsLookupService();
        var webSearch = new FakeWebSearchService();
        var service = CreateService(parser, lookup, webSearch: webSearch);

        await service.SearchAsync("tanuki arigato set half serving", CancellationToken.None);

        Assert.Empty(lookup.Queries);
        Assert.Single(webSearch.Queries);
        Assert.Contains("tanuki arigato set", webSearch.Queries.Single(), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("nutrition", webSearch.Queries.Single(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TavilyQueryBuilder_UsesMillilitersAndServingForBeverages()
    {
        var query = new TavilyQueryBuilder().Build(new FoodUnit
        {
            ProductName = "coffee latte", Unit = "serving", Kind = FoodUnitKind.PreparedFood
        });

        Assert.Contains("100", query, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("100 g", query, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SearchAsync_WhenOpenFoodFactsIsEmptyForMassMarketProduct_UsesWebFallback()
    {
        var parser = new FakeFoodInputParser(new[]
        {
            new FoodUnit { ProductName = "semolina porridge", Unit = "g", Kind = FoodUnitKind.MassMarketProduct }
        });
        var lookup = new FakeNutritionFactsLookupService { Results = Array.Empty<ProductNutritionDto>() };
        var webSearch = new FakeWebSearchService
        {
            Results = new[] { new WebSearchResult("Semolina", new Uri("https://example.com/semolina"), "nutrition", 1) }
        };
        var extractor = new FakeEvidenceExtractor
        {
            Candidates = new[] { new ProductNutritionDto { ProductId = "WEB:1", ProductName = "Semolina", SourceType = "WebSearch" } }
        };
        var service = CreateService(parser, lookup, webSearch: webSearch, extractor: extractor);

        var result = await service.SearchAsync("semolina porridge 200 g", CancellationToken.None);

        Assert.Single(webSearch.Queries);
        Assert.Equal("WEB:1", result.Clarifications.Single().Candidates.Single().ProductId);
    }

    [Fact]
    public async Task SearchAsync_WhenAllSourcesAreEmpty_ReturnsNotFoundResponse()
    {
        var parser = new FakeFoodInputParser(new[]
        {
            new FoodUnit { ProductName = "semolina porridge", Unit = "g", Kind = FoodUnitKind.MassMarketProduct }
        });
        var lookup = new FakeNutritionFactsLookupService { Results = Array.Empty<ProductNutritionDto>() };
        var service = CreateService(parser, lookup);

        var result = await service.SearchAsync("semolina porridge 200 g", CancellationToken.None);

        Assert.Empty(result.Clarifications);
    }

    private static NutritionChatQueryService CreateService(IFoodInputParser parser,
        FakeNutritionFactsLookupService lookup,
        FakeWebSearchService? webSearch = null, FakeEvidenceExtractor? extractor = null)
        => new(parser, lookup, webSearch ?? new FakeWebSearchService(), new TavilyQueryBuilder(),
            extractor ?? new FakeEvidenceExtractor(),
            NullLogger<NutritionChatQueryService>.Instance);

    private sealed class FakeFoodInputParser : IFoodInputParser
    {
        private readonly IReadOnlyCollection<FoodUnit> _foodUnits;

        public FakeFoodInputParser(IReadOnlyCollection<FoodUnit> foodUnits)
        {
            _foodUnits = foodUnits;
        }

        public Task<IReadOnlyCollection<FoodUnit>> ParseAsync(string userInput, CancellationToken cancellationToken)
        {
            return Task.FromResult(_foodUnits);
        }
    }

    private sealed class
        FakeNutritionFactsLookupService : Nutrition.Application.Abstractions.Services.INutritionFactsLookupService
    {
        private readonly List<string> _queries = new();

        public IReadOnlyCollection<string> Queries => _queries;

        public IReadOnlyCollection<ProductNutritionDto>? Results { get; init; }

        public Task<IReadOnlyCollection<ProductNutritionDto>> SearchAsync(string query,
            CancellationToken cancellationToken)
        {
            _queries.Add(query);

            IReadOnlyCollection<ProductNutritionDto> results = Results ?? Enumerable.Range(1, 5).Select(index
                => new ProductNutritionDto
                {
                    ProductId = $"{query}-{index}",
                    ProductName = $"{query} {index}",
                    NutritionFacts = new NutritionFactsDto { Calories = index },
                    SourceType = "Test",
                    SourceReference = "Test",
                    ConfidenceScore = 1
                }).ToArray();

            return Task.FromResult(results);
        }
    }
    private sealed class FakeWebSearchService : IWebSearchService
    {
        private readonly List<string> _queries = new();

        public IReadOnlyCollection<string> Queries => _queries;

        public WebSearchDepth? LastDepth { get; private set; }

        public IReadOnlyCollection<WebSearchResult> Results { get; init; } = Array.Empty<WebSearchResult>();

        public Task<WebSearchResponse> SearchAsync(WebSearchRequest request, CancellationToken cancellationToken)
        {
            _queries.Add(request.Query);
            LastDepth = request.Depth;
            return Task.FromResult(new WebSearchResponse(Results, null, null));
        }
    }

    private sealed class FakeEvidenceExtractor : INutritionEvidenceExtractor
    {
        public IReadOnlyCollection<ProductNutritionDto> Candidates { get; init; } = Array.Empty<ProductNutritionDto>();

        public Task<IReadOnlyCollection<ProductNutritionDto>> ExtractAsync(FoodUnit foodUnit,
            IReadOnlyCollection<WebSearchResult> sources, CancellationToken cancellationToken)
            => Task.FromResult(Candidates);
    }
}
