using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Nutrition.Application.Abstractions.Services;
using Nutrition.Infrastructure.Agent;
using Nutrition.Infrastructure.Agent.NutritionLookup;
using Nutrition.Infrastructure.Agent.Parsing;
using Nutrition.Infrastructure.Agent.WebSearch;
using Nutrition.Shared.Dtos;

namespace Nutrition.Application.Tests;

public sealed class NutritionAgentTests
{
    [Fact]
    public async Task MafFoodInputParser_SplitsCompoundMealIntoFoodUnits()
    {
        const string responseJson = """
                                    {
                                      "items": [
                                        { "productName": "pasta", "quantity": 1, "unit": "serving", "brand": null, "preparation": null, "kind": "MassMarketProduct" },
                                        { "productName": "chicken", "quantity": 1, "unit": "serving", "brand": null, "preparation": null, "kind": "MassMarketProduct" }
                                      ]
                                    }
                                    """;

        var parser = new MafFoodInputParser(new FakeChatClient(responseJson));

        var result = await parser.ParseAsync("pasta with chicken", CancellationToken.None);

        Assert.Collection(result,
            item =>
            {
                Assert.Equal("pasta", item.ProductName);
                Assert.Equal(FoodUnitKind.MassMarketProduct, item.Kind);
            },
            item =>
            {
                Assert.Equal("chicken", item.ProductName);
                Assert.Equal(FoodUnitKind.MassMarketProduct, item.Kind);
            });
    }

    [Fact]
    public async Task MafFoodInputParser_PreservesBrandAndPreparedFoodKind()
    {
        const string responseJson = """
                                    {
                                      "items": [
                                        {
                                          "productName": "гаспачо",
                                          "quantity": 1,
                                          "unit": "serving",
                                          "brand": "creative kitchen самокат",
                                          "preparation": null,
                                          "kind": "PreparedFood"
                                        }
                                      ]
                                    }
                                    """;

        var parser = new MafFoodInputParser(new FakeChatClient(responseJson));

        var result = await parser.ParseAsync("гаспачо creative kitchen самокат", CancellationToken.None);

        var item = Assert.Single(result);
        Assert.Equal("гаспачо", item.ProductName);
        Assert.Equal("creative kitchen самокат", item.Brand);
        Assert.Equal(FoodUnitKind.PreparedFood, item.Kind);
    }

    [Fact]
    public async Task MafNutritionEvidenceExtractor_ReturnsCompletePerServingCandidate()
    {
        const string responseJson = """
                                    {
                                      "candidates": [
                                        {
                                          "productName": "гаспачо",
                                          "brand": "creative kitchen самокат",
                                          "servingSize": 320,
                                          "servingUnit": "g",
                                          "valueBasis": "PerServing",
                                          "calories": 110,
                                          "protein": 3,
                                          "fat": 5,
                                          "carbs": 14,
                                          "sourceUrl": "https://example.com/gazpacho",
                                          "sourceIds": ["S1"],
                                          "isExactProductMatch": true,
                                          "valuesExplicitlyStated": true,
                                          "confidence": 0.91,
                                          "warnings": []
                                        }
                                      ]
                                    }
                                    """;
        var extractor = new MafNutritionEvidenceExtractor(new FakeChatClient(responseJson));
        var foodUnit = new FoodUnit
        {
            ProductName = "гаспачо",
            Brand = "creative kitchen самокат",
            Quantity = 1,
            Unit = "serving",
            Kind = FoodUnitKind.PreparedFood
        };
        var sources = new[]
        {
            new WebSearchResult("Гаспачо", new Uri("https://example.com/gazpacho"), "КБЖУ", 0.9m)
        };

        var result = await extractor.ExtractAsync(foodUnit, sources, CancellationToken.None);

        var candidate = Assert.Single(result);
        Assert.Equal("WebSearch", candidate.SourceType);
        Assert.Equal("PerServing", candidate.NutritionValueBasis);
        Assert.Equal(320, candidate.ServingSize);
        Assert.Equal(110, candidate.NutritionFacts.Calories);
        Assert.Equal(3, candidate.NutritionFacts.Protein);
        Assert.Equal(5, candidate.NutritionFacts.Fat);
        Assert.Equal(14, candidate.NutritionFacts.Carbs);
    }

    [Fact]
    public async Task MafNutritionEvidenceExtractor_DropsCandidate_WhenMacrosAreIncomplete()
    {
        const string responseJson = """
                                    {
                                      "candidates": [
                                        {
                                          "productName": "гаспачо",
                                          "brand": "creative kitchen самокат",
                                          "servingSize": 320,
                                          "servingUnit": "g",
                                          "valueBasis": "PerServing",
                                          "calories": 110,
                                          "protein": null,
                                          "fat": 5,
                                          "carbs": 14,
                                          "sourceUrl": "https://example.com/gazpacho",
                                          "sourceIds": ["S1"],
                                          "isExactProductMatch": true,
                                          "valuesExplicitlyStated": true,
                                          "confidence": 0.91,
                                          "warnings": []
                                        }
                                      ]
                                    }
                                    """;
        var extractor = new MafNutritionEvidenceExtractor(new FakeChatClient(responseJson));
        var foodUnit = new FoodUnit
        {
            ProductName = "гаспачо",
            Brand = "creative kitchen самокат",
            Quantity = 1,
            Unit = "serving",
            Kind = FoodUnitKind.PreparedFood
        };
        var sources = new[]
        {
            new WebSearchResult("Гаспачо", new Uri("https://example.com/gazpacho"), "КБЖУ", 0.9m)
        };

        var result = await extractor.ExtractAsync(foodUnit, sources, CancellationToken.None);

        Assert.Empty(result);
    }

    [Fact]
    public async Task MafNutritionEvidenceExtractor_FallsBackToStructuredSnippet_WhenLlmReturnsNoCandidates()
    {
        const string responseJson = """{ "candidates": [] }""";
        var extractor = new MafNutritionEvidenceExtractor(new FakeChatClient(responseJson));
        var foodUnit = new FoodUnit
        {
            ProductName = "аригато сет",
            Brand = "тануки",
            Quantity = 1,
            Unit = "serving",
            Kind = FoodUnitKind.PreparedFood
        };
        var sources = new[]
        {
            new WebSearchResult(
                "Калории и Пищевая Информация Тануки Аригато Сет - Fatsecret.ru",
                new Uri("https://www.fatsecret.ru/search?q=tanuki-arigato-set"),
                "в 1 порция (910г) - Калории: 2310ккал | Жир: 107,00г | Углев: 264,00г | Белк: 73,00г. Похожие · Ролл Аригато (Тануки).",
                0.87m)
        };

        var result = await extractor.ExtractAsync(foodUnit, sources, CancellationToken.None);

        var candidate = Assert.Single(result);
        Assert.Equal("WebSearch", candidate.SourceType);
        Assert.Equal("PerServing", candidate.NutritionValueBasis);
        Assert.Equal(910, candidate.ServingSize);
        Assert.Equal("g", candidate.ServingUnit);
        Assert.Equal(2310, candidate.NutritionFacts.Calories);
        Assert.Equal(73, candidate.NutritionFacts.Protein);
        Assert.Equal(107, candidate.NutritionFacts.Fat);
        Assert.Equal(264, candidate.NutritionFacts.Carbs);
    }

    [Fact]
    public async Task MafNutritionEvidenceExtractor_FallsBackToCompactFatSecretSnippet_WhenLlmReturnsNoCandidates()
    {
        const string responseJson = """{ "candidates": [] }""";
        var extractor = new MafNutritionEvidenceExtractor(new FakeChatClient(responseJson));
        var foodUnit = new FoodUnit
        {
            ProductName = "аригато сет",
            Brand = "тануки",
            Quantity = 1,
            Unit = "serving",
            Kind = FoodUnitKind.PreparedFood
        };
        var sources = new[]
        {
            new WebSearchResult(
                "Тануки Аригато Сет Калории и Пищевая Ценность",
                new Uri("https://www.fatsecret.ru/calories-nutrition/tanuki/arigato-set/1-serving"),
                "Тануки Аригато Сет. Тануки. Аригато Сет. Кал. 2310. Жир. 107 г. Углев. 264 г. Белк. 73 г. 1 порция (910 г) содержит 2310 калорий. Источник · fatsecret Platform",
                0.87m)
        };

        var result = await extractor.ExtractAsync(foodUnit, sources, CancellationToken.None);

        var candidate = Assert.Single(result);
        Assert.Equal("WebSearch", candidate.SourceType);
        Assert.Equal("PerServing", candidate.NutritionValueBasis);
        Assert.Equal(910, candidate.ServingSize);
        Assert.Equal("g", candidate.ServingUnit);
        Assert.Equal(2310, candidate.NutritionFacts.Calories);
        Assert.Equal(73, candidate.NutritionFacts.Protein);
        Assert.Equal(107, candidate.NutritionFacts.Fat);
        Assert.Equal(264, candidate.NutritionFacts.Carbs);
    }

    [Fact]
    public async Task MafNutritionEvidenceExtractor_FallsBackToOfficialTanukiSnippet_WhenBrandIsInUrl()
    {
        const string responseJson = """{ "candidates": [] }""";
        var extractor = new MafNutritionEvidenceExtractor(new FakeChatClient(responseJson));
        var foodUnit = new FoodUnit
        {
            ProductName = "аригато сет",
            Brand = "тануки",
            Quantity = 1,
            Unit = "serving",
            Kind = FoodUnitKind.PreparedFood
        };
        var sources = new[]
        {
            new WebSearchResult(
                "Аригато сет заказать с доставкой домой и в офис из ...",
                new Uri("https://tanukifamily.ru/tanuki/product/arigato-set"),
                "и имбирь (2 шт.). 30 роллов для вечера под сериал. Пищевая ценность на 910 г. белки. 73 г. жиры. 107 г. Углеводы. 264 г. Энерг. ценн. 2310 ккал. Входит в заказ.Read more",
                0.88m)
        };

        var result = await extractor.ExtractAsync(foodUnit, sources, CancellationToken.None);

        var candidate = Assert.Single(result);
        Assert.Equal("https://tanukifamily.ru/tanuki/product/arigato-set", candidate.SourceReference);
        Assert.Equal(910, candidate.ServingSize);
        Assert.Equal(2310, candidate.NutritionFacts.Calories);
        Assert.Equal(73, candidate.NutritionFacts.Protein);
        Assert.Equal(107, candidate.NutritionFacts.Fat);
        Assert.Equal(264, candidate.NutritionFacts.Carbs);
    }

    [Fact]
    public async Task NutritionChatQueryService_ReturnsClarificationForEachParsedFoodUnit()
    {
        var parser = new FakeFoodInputParser(new[]
        {
            new FoodUnit { ProductName = "pasta", Quantity = 1, Unit = "serving", Kind = FoodUnitKind.MassMarketProduct },
            new FoodUnit { ProductName = "chicken", Quantity = 1, Unit = "serving", Kind = FoodUnitKind.MassMarketProduct }
        });
        var lookup = new FakeNutritionFactsLookupService();
        var service = new NutritionChatQueryService(
            parser,
            lookup,
            new FakeOpenFoodFactsCandidateJudge(),
            new FakeWebSearchService(),
            new TavilyQueryBuilder(),
            new FakeEvidenceExtractor(),
            NullLogger<NutritionChatQueryService>.Instance);

        var result = await service.SearchAsync("pasta with chicken", CancellationToken.None);

        Assert.Equal(new[] { "pasta", "chicken" }, lookup.Queries);
        Assert.True(result.RequiresClarification);
        Assert.Empty(result.Items);
        Assert.Equal(2, result.Clarifications.Count);
        Assert.All(result.Clarifications, clarification => Assert.Equal(3, clarification.Candidates.Count));
    }

    private sealed class FakeChatClient : IChatClient
    {
        private readonly string _response;

        public FakeChatClient(string response)
        {
            _response = response;
        }

        public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, _response)));
        }

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            yield return new ChatResponseUpdate(ChatRole.Assistant, _response);
        }

        public object? GetService(Type serviceType, object? serviceKey = null)
        {
            return serviceType.IsInstanceOfType(this) ? this : null;
        }

        public void Dispose()
        {
        }
    }

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

    private sealed class FakeNutritionFactsLookupService : INutritionFactsLookupService
    {
        private readonly List<string> _queries = new();

        public IReadOnlyCollection<string> Queries => _queries;

        public Task<IReadOnlyCollection<ProductNutritionDto>> SearchAsync(string query,
            CancellationToken cancellationToken)
        {
            _queries.Add(query);

            IReadOnlyCollection<ProductNutritionDto> results = Enumerable.Range(1, 5).Select(index
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

    private sealed class FakeOpenFoodFactsCandidateJudge : IOpenFoodFactsCandidateJudge
    {
        public Task<IReadOnlyCollection<ProductNutritionDto>> SelectAcceptableAsync(
            FoodUnit foodUnit,
            IReadOnlyCollection<ProductNutritionDto> candidates,
            CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyCollection<ProductNutritionDto>>(Array.Empty<ProductNutritionDto>());
    }

    private sealed class FakeWebSearchService : IWebSearchService
    {
        public Task<WebSearchResponse> SearchAsync(WebSearchRequest request, CancellationToken cancellationToken)
            => Task.FromResult(new WebSearchResponse(Array.Empty<WebSearchResult>(), null, null));
    }

    private sealed class FakeEvidenceExtractor : INutritionEvidenceExtractor
    {
        public Task<IReadOnlyCollection<ProductNutritionDto>> ExtractAsync(
            FoodUnit foodUnit,
            IReadOnlyCollection<WebSearchResult> sources,
            CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyCollection<ProductNutritionDto>>(Array.Empty<ProductNutritionDto>());
    }
}
