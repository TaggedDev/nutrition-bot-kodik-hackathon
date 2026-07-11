using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text.Json;
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

        Assert.Collection(result, item =>
        {
            Assert.Equal("pasta", item.ProductName);
            Assert.Equal(FoodUnitKind.MassMarketProduct, item.Kind);
        }, item =>
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
                                          "productName": "РіР°СЃРїР°С‡Рѕ",
                                          "quantity": 1,
                                          "unit": "serving",
                                          "brand": "creative kitchen СЃР°РјРѕРєР°С‚",
                                          "preparation": null,
                                          "kind": "PreparedFood"
                                        }
                                      ]
                                    }
                                    """;

        var parser = new MafFoodInputParser(new FakeChatClient(responseJson));

        var result = await parser.ParseAsync("РіР°СЃРїР°С‡Рѕ creative kitchen СЃР°РјРѕРєР°С‚", CancellationToken.None);

        var item = Assert.Single(result);
        Assert.Equal("РіР°СЃРїР°С‡Рѕ", item.ProductName);
        Assert.Equal("creative kitchen СЃР°РјРѕРєР°С‚", item.Brand);
        Assert.Equal(FoodUnitKind.PreparedFood, item.Kind);
    }

    [Fact]
    public async Task MafNutritionEvidenceExtractor_ReturnsCompletePerServingCandidate()
    {
        const string responseJson = """
                                    {
                                      "candidates": [
                                        {
                                          "productName": "РіР°СЃРїР°С‡Рѕ",
                                          "brand": "creative kitchen СЃР°РјРѕРєР°С‚",
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
            ProductName = "РіР°СЃРїР°С‡Рѕ",
            Brand = "creative kitchen СЃР°РјРѕРєР°С‚",
            Quantity = 1,
            Unit = "serving",
            Kind = FoodUnitKind.PreparedFood
        };
        var sources = new[] { new WebSearchResult("Р“Р°СЃРїР°С‡Рѕ", new Uri("https://example.com/gazpacho"), "РљР‘Р–РЈ", 0.9m) };

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
    public async Task MafNutritionEvidenceExtractor_MapsCandidate_WhenMacrosAreIncomplete()
    {
        const string responseJson = """
                                    {
                                      "candidates": [
                                        {
                                          "productName": "РіР°СЃРїР°С‡Рѕ",
                                          "brand": "creative kitchen СЃР°РјРѕРєР°С‚",
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
            ProductName = "РіР°СЃРїР°С‡Рѕ",
            Brand = "creative kitchen СЃР°РјРѕРєР°С‚",
            Quantity = 1,
            Unit = "serving",
            Kind = FoodUnitKind.PreparedFood
        };
        var sources = new[] { new WebSearchResult("Р“Р°СЃРїР°С‡Рѕ", new Uri("https://example.com/gazpacho"), "РљР‘Р–РЈ", 0.9m) };

        var result = await extractor.ExtractAsync(foodUnit, sources, CancellationToken.None);

        var candidate = Assert.Single(result);
        Assert.Equal(110, candidate.NutritionFacts.Calories);
        Assert.Equal(0, candidate.NutritionFacts.Protein);
        Assert.Equal(5, candidate.NutritionFacts.Fat);
        Assert.Equal(14, candidate.NutritionFacts.Carbs);
    }

    [Fact]
    public async Task MafNutritionEvidenceExtractor_RetriesInvalidJson_AndReturnsThirdAttempt()
    {
        var validJson = ExtractorResponse("beef", 145.4m, 0m, 0m, 0m, "https://example.com/beef");
        var chatClient = new FakeChatClient("not-json", "not-json", validJson);
        var extractor = new MafNutritionEvidenceExtractor(chatClient);
        var sources = new[]
        {
            new WebSearchResult("Beef", new Uri("https://example.com/beef"), "145.4 kcal", 0.9m)
        };

        var result = await extractor.ExtractAsync(new FoodUnit { ProductName = "beef", Unit = "g" }, sources,
            CancellationToken.None);

        Assert.Equal(3, chatClient.CallCount);
        Assert.Equal(145.4m, Assert.Single(result).NutritionFacts.Calories);
    }

    [Fact]
    public async Task MafNutritionEvidenceExtractor_MapsStructuredPerServingCandidate()
    {
        var responseJson = ExtractorResponse("arigato set", 2310, 73, 107, 264, "https://www.fatsecret.ru/search?q=tanuki-arigato-set", "PerServing", 910);
        var extractor = new MafNutritionEvidenceExtractor(new FakeChatClient(responseJson));
        var foodUnit = new FoodUnit
        {
            ProductName = "Р°СЂРёРіР°С‚Рѕ СЃРµС‚",
            Brand = "С‚Р°РЅСѓРєРё",
            Quantity = 1,
            Unit = "serving",
            Kind = FoodUnitKind.PreparedFood
        };
        var sources = new[]
        {
            new WebSearchResult("РљР°Р»РѕСЂРёРё Рё РџРёС‰РµРІР°СЏ РРЅС„РѕСЂРјР°С†РёСЏ РўР°РЅСѓРєРё РђСЂРёРіР°С‚Рѕ РЎРµС‚ - Fatsecret.ru",
                new Uri("https://www.fatsecret.ru/search?q=tanuki-arigato-set"),
                "РІ 1 РїРѕСЂС†РёСЏ (910Рі) - РљР°Р»РѕСЂРёРё: 2310РєРєР°Р» | Р–РёСЂ: 107,00Рі | РЈРіР»РµРІ: 264,00Рі | Р‘РµР»Рє: 73,00Рі. РџРѕС…РѕР¶РёРµ В· Р РѕР»Р» РђСЂРёРіР°С‚Рѕ (РўР°РЅСѓРєРё).",
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
    public async Task MafNutritionEvidenceExtractor_MapsCompactPerServingCandidate()
    {
        var responseJson = ExtractorResponse("arigato set", 2310, 73, 107, 264, "https://www.fatsecret.ru/calories-nutrition/tanuki/arigato-set/1-serving", "PerServing", 910);
        var extractor = new MafNutritionEvidenceExtractor(new FakeChatClient(responseJson));
        var foodUnit = new FoodUnit
        {
            ProductName = "Р°СЂРёРіР°С‚Рѕ СЃРµС‚",
            Brand = "С‚Р°РЅСѓРєРё",
            Quantity = 1,
            Unit = "serving",
            Kind = FoodUnitKind.PreparedFood
        };
        var sources = new[]
        {
            new WebSearchResult("РўР°РЅСѓРєРё РђСЂРёРіР°С‚Рѕ РЎРµС‚ РљР°Р»РѕСЂРёРё Рё РџРёС‰РµРІР°СЏ Р¦РµРЅРЅРѕСЃС‚СЊ",
                new Uri("https://www.fatsecret.ru/calories-nutrition/tanuki/arigato-set/1-serving"),
                "РўР°РЅСѓРєРё РђСЂРёРіР°С‚Рѕ РЎРµС‚. РўР°РЅСѓРєРё. РђСЂРёРіР°С‚Рѕ РЎРµС‚. РљР°Р». 2310. Р–РёСЂ. 107 Рі. РЈРіР»РµРІ. 264 Рі. Р‘РµР»Рє. 73 Рі. 1 РїРѕСЂС†РёСЏ (910 Рі) СЃРѕРґРµСЂР¶РёС‚ 2310 РєР°Р»РѕСЂРёР№. РСЃС‚РѕС‡РЅРёРє В· fatsecret Platform",
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
    public async Task MafNutritionEvidenceExtractor_MapsCommonRussianPer100GramsCandidate()
    {
        var responseJson = ExtractorResponse("semolina porridge", 98, 3.0m, 3.2m, 15.3m, "https://example.com/semolina");
        var extractor = new MafNutritionEvidenceExtractor(new FakeChatClient(responseJson));
        var foodUnit = new FoodUnit { ProductName = "РјР°РЅРЅР°СЏ РєР°С€Р°", Unit = "g" };
        var sources = new[]
        {
            new WebSearchResult("РњР°РЅРЅР°СЏ РєР°С€Р° РљР‘Р–РЈ", new Uri("https://example.com/semolina"),
                "РњР°РЅРЅР°СЏ РєР°С€Р° РЅР° РјРѕР»РѕРєРµ, СЃРѕРґРµСЂР¶Р°РЅРёРµ Р‘Р–РЈ РЅР° 100 Рі - 3.0 Рі Р±РµР»РєР°, 3.2 Рі Р¶РёСЂРѕРІ, 15.3 Рі СѓРіР»РµРІРѕРґРѕРІ, 98 РєРєР°Р».",
                0.9m)
        };

        var candidate = Assert.Single(await extractor.ExtractAsync(foodUnit, sources, CancellationToken.None));

        Assert.Equal("Per100Grams", candidate.NutritionValueBasis);
        Assert.Equal(98, candidate.NutritionFacts.Calories);
        Assert.Equal(3.0m, candidate.NutritionFacts.Protein);
        Assert.Equal(3.2m, candidate.NutritionFacts.Fat);
        Assert.Equal(15.3m, candidate.NutritionFacts.Carbs);
    }

    [Fact]
    public async Task MafNutritionEvidenceExtractor_MapsPer100GramsCandidate()
    {
        var responseJson = ExtractorResponse("curd", 159, 16.7m, 9, 2, "https://example.com/curd-9");
        var extractor = new MafNutritionEvidenceExtractor(new FakeChatClient(responseJson));
        var foodUnit = new FoodUnit { ProductName = "С‚РІРѕСЂРѕРі", Unit = "g" };
        var sources = new[]
        {
            new WebSearchResult("РўРІРѕСЂРѕРі 9% Р‘Р–РЈ РЅР° 100 РіСЂР°РјРј", new Uri("https://example.com/curd-9"),
                "РљР°Р»РѕСЂРёР№РЅРѕСЃС‚СЊ, 159 РєРљР°Р»; Р‘РµР»РєРё, 16.7 Рі; Р–РёСЂС‹, 9 Рі; РЈРіР»РµРІРѕРґС‹, 2 Рі", 0.9m)
        };

        var candidate = Assert.Single(await extractor.ExtractAsync(foodUnit, sources, CancellationToken.None));

        Assert.Equal("Per100Grams", candidate.NutritionValueBasis);
        Assert.Equal(100, candidate.ServingSize);
        Assert.Equal(159, candidate.NutritionFacts.Calories);
        Assert.Equal(16.7m, candidate.NutritionFacts.Protein);
        Assert.Equal(9, candidate.NutritionFacts.Fat);
        Assert.Equal(2, candidate.NutritionFacts.Carbs);
    }

    [Fact]
    public async Task MafNutritionEvidenceExtractor_MapsPer100GramsCandidateWithCaloriesBeforeMacros()
    {
        var responseJson = ExtractorResponse("curd", 121, 16.7m, 5, 2.8m, "https://example.com/curd-5");
        var extractor = new MafNutritionEvidenceExtractor(new FakeChatClient(responseJson));
        var sources = new[]
        {
            new WebSearchResult("РўРІРѕСЂРѕРі 5%", new Uri("https://example.com/curd-5"),
                "РљР°Р»РѕСЂРёР№РЅРѕСЃС‚СЊ: 121 РєРєР°Р»/100 Рі Р‘РµР»РєРё: 16,7 Рі Р–РёСЂС‹: 5 Рі РЈРіР»РµРІРѕРґС‹: 2,8 Рі", 0.9m)
        };

        var candidate = Assert.Single(await extractor.ExtractAsync(new FoodUnit { ProductName = "С‚РІРѕСЂРѕРі", Unit = "g" },
            sources, CancellationToken.None));

        Assert.Equal("Per100Grams", candidate.NutritionValueBasis);
        Assert.Equal(121, candidate.NutritionFacts.Calories);
        Assert.Equal(16.7m, candidate.NutritionFacts.Protein);
        Assert.Equal(5, candidate.NutritionFacts.Fat);
        Assert.Equal(2.8m, candidate.NutritionFacts.Carbs);
    }

    [Fact]
    public async Task MafNutritionEvidenceExtractor_MapsPer100GramsTableCandidate()
    {
        var responseJson = ExtractorResponse("latte", 114, 7.45m, 4.66m, 10.45m, "https://example.com/latte");
        var extractor = new MafNutritionEvidenceExtractor(new FakeChatClient(responseJson));
        var sources = new[]
        {
            new WebSearchResult("РљРѕС„Рµ Р›Р°С‚С‚Рµ Р±РѕР»СЊС€РѕР№ 400 РјР»", new Uri("https://example.com/latte"),
                "РќР° 100 Рі РїСЂРѕРґСѓРєС‚Р°; Р‘РµР»РєРѕРІ, 7.45 Рі, 11%; Р–РёСЂРѕРІ, 4.66 Рі, 6%; РЈРіР»РµРІРѕРґРѕРІ, 10.45 Рі, 3%; РљР°Р»РѕСЂРёР№РЅРѕСЃС‚СЊ, 114.00 РєРєР°Р», 5%",
                0.9m)
        };

        var candidate = Assert.Single(await extractor.ExtractAsync(
            new FoodUnit { ProductName = "РєРѕС„Рµ Р»Р°С‚С‚Рµ", Unit = "serving", Kind = FoodUnitKind.PreparedFood }, sources,
            CancellationToken.None));

        Assert.Equal("Per100Grams", candidate.NutritionValueBasis);
        Assert.Equal(114, candidate.NutritionFacts.Calories);
        Assert.Equal(7.45m, candidate.NutritionFacts.Protein);
        Assert.Equal(4.66m, candidate.NutritionFacts.Fat);
        Assert.Equal(10.45m, candidate.NutritionFacts.Carbs);
    }

    [Fact]
    public async Task MafNutritionEvidenceExtractor_MapsOfficialTanukiCandidate()
    {
        var responseJson = ExtractorResponse("arigato set", 2310, 73, 107, 264, "https://tanukifamily.ru/tanuki/product/arigato-set", "PerServing", 910);
        var extractor = new MafNutritionEvidenceExtractor(new FakeChatClient(responseJson));
        var foodUnit = new FoodUnit
        {
            ProductName = "Р°СЂРёРіР°С‚Рѕ СЃРµС‚",
            Brand = "С‚Р°РЅСѓРєРё",
            Quantity = 1,
            Unit = "serving",
            Kind = FoodUnitKind.PreparedFood
        };
        var sources = new[]
        {
            new WebSearchResult("РђСЂРёРіР°С‚Рѕ СЃРµС‚ Р·Р°РєР°Р·Р°С‚СЊ СЃ РґРѕСЃС‚Р°РІРєРѕР№ РґРѕРјРѕР№ Рё РІ РѕС„РёСЃ РёР· ...",
                new Uri("https://tanukifamily.ru/tanuki/product/arigato-set"),
                "Рё РёРјР±РёСЂСЊ (2 С€С‚.). 30 СЂРѕР»Р»РѕРІ РґР»СЏ РІРµС‡РµСЂР° РїРѕРґ СЃРµСЂРёР°Р». РџРёС‰РµРІР°СЏ С†РµРЅРЅРѕСЃС‚СЊ РЅР° 910 Рі. Р±РµР»РєРё. 73 Рі. Р¶РёСЂС‹. 107 Рі. РЈРіР»РµРІРѕРґС‹. 264 Рі. Р­РЅРµСЂРі. С†РµРЅРЅ. 2310 РєРєР°Р». Р’С…РѕРґРёС‚ РІ Р·Р°РєР°Р·.Read more",
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
            new FoodUnit
            {
                ProductName = "pasta", Quantity = 1, Unit = "serving", Kind = FoodUnitKind.MassMarketProduct
            },
            new FoodUnit
            {
                ProductName = "chicken", Quantity = 1, Unit = "serving", Kind = FoodUnitKind.MassMarketProduct
            }
        });
        var lookup = new FakeNutritionFactsLookupService();
        var service = new NutritionChatQueryService(parser, lookup, new FakeWebSearchService(),
            new TavilyQueryBuilder(), new FakeEvidenceExtractor(),
            NullLogger<NutritionChatQueryService>.Instance);

        var result = await service.SearchAsync("pasta with chicken", CancellationToken.None);

        Assert.Equal(new[] { "pasta", "chicken" }, lookup.Queries);
        Assert.True(result.RequiresClarification);
        Assert.Empty(result.Items);
        Assert.Equal(2, result.Clarifications.Count);
        Assert.All(result.Clarifications, clarification => Assert.Equal(3, clarification.Candidates.Count));
    }

    private static string ExtractorResponse(string name, decimal calories, decimal protein, decimal fat,
        decimal carbs, string url, string basis = "Per100Grams", decimal? servingSize = 100,
        string servingUnit = "g")
        => JsonSerializer.Serialize(new
        {
            candidates = new[]
            {
                new
                {
                    productName = name,
                    brand = (string?)null,
                    servingSize,
                    servingUnit,
                    valueBasis = basis,
                    calories,
                    protein,
                    fat,
                    carbs,
                    sourceUrl = url,
                    confidence = 0.9m
                }
            }
        }, new JsonSerializerOptions(JsonSerializerDefaults.Web));

    private sealed class FakeChatClient : IChatClient
    {
        private readonly Queue<string> _responses;
        private string _lastResponse;

        public FakeChatClient(params string[] responses)
        {
            _responses = new Queue<string>(responses);
            _lastResponse = responses.LastOrDefault() ?? "{ \"items\": [] }";
        }

        public int CallCount { get; private set; }

        public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            if (_responses.TryDequeue(out var response))
            {
                _lastResponse = response;
            }

            return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, _lastResponse)));
        }

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            yield return new ChatResponseUpdate(ChatRole.Assistant, _lastResponse);
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

    private sealed class FakeWebSearchService : IWebSearchService
    {
        public Task<WebSearchResponse> SearchAsync(WebSearchRequest request, CancellationToken cancellationToken)
            => Task.FromResult(new WebSearchResponse(Array.Empty<WebSearchResult>(), null, null));
    }

    private sealed class FakeEvidenceExtractor : INutritionEvidenceExtractor
    {
        public Task<IReadOnlyCollection<ProductNutritionDto>> ExtractAsync(FoodUnit foodUnit,
            IReadOnlyCollection<WebSearchResult> sources, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyCollection<ProductNutritionDto>>(Array.Empty<ProductNutritionDto>());
    }
}
