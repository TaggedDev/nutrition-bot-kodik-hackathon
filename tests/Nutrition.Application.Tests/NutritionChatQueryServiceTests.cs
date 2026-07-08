using Nutrition.Infrastructure.Agent;
using Nutrition.Shared.Dtos;

namespace Nutrition.Application.Tests;

public sealed class NutritionChatQueryServiceTests
{
    [Fact]
    public async Task SearchAsync_ReturnsTrimmedQuery_WhenParserReturnsNoUnits()
    {
        var parser = new FakeFoodInputParser(Array.Empty<FoodUnit>());
        var lookup = new FakeNutritionFactsLookupService();
        var service = new NutritionChatQueryService(parser, lookup);

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
            new FoodUnit { ProductName = "pasta", Quantity = 1, Unit = "serving" },
            new FoodUnit { ProductName = " pasta ", Quantity = 2, Unit = "serving" },
            new FoodUnit { ProductName = "chicken", Quantity = 1, Unit = "serving" },
            new FoodUnit { ProductName = " ", Quantity = 1, Unit = "serving" }
        });
        var lookup = new FakeNutritionFactsLookupService();
        var service = new NutritionChatQueryService(parser, lookup);

        var result = await service.SearchAsync("pasta with chicken", CancellationToken.None);

        Assert.Equal(new[] { "pasta", "chicken" }, lookup.Queries);
        Assert.True(result.RequiresClarification);
        Assert.Empty(result.Items);
        Assert.Equal(2, result.Clarifications.Count);
        Assert.All(result.Clarifications, clarification => Assert.Equal(3, clarification.Candidates.Count));
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

    private sealed class
        FakeNutritionFactsLookupService : Nutrition.Application.Abstractions.Services.INutritionFactsLookupService
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
}