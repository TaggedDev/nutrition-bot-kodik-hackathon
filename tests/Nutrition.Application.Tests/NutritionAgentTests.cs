using Microsoft.Extensions.AI;
using Nutrition.Application.Abstractions.Services;
using Nutrition.Infrastructure.Agent;
using Nutrition.Infrastructure.Agent.Parsing;
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
            { "productName": "pasta", "quantity": 1, "unit": "serving", "brand": null, "preparation": null },
            { "productName": "chicken", "quantity": 1, "unit": "serving", "brand": null, "preparation": null }
          ]
        }
        """;

        var parser = new MafFoodInputParser(new FakeChatClient(responseJson));

        var result = await parser.ParseAsync("макароны с курицей", CancellationToken.None);

        Assert.Collection(
            result,
            item => Assert.Equal("pasta", item.ProductName),
            item => Assert.Equal("chicken", item.ProductName));
    }

    [Fact]
    public async Task NutritionChatQueryService_SearchesEachParsedFoodUnit_AndReturnsTopThree()
    {
        var parser = new FakeFoodInputParser(new[]
        {
            new FoodUnit { ProductName = "pasta", Quantity = 1, Unit = "serving" },
            new FoodUnit { ProductName = "chicken", Quantity = 1, Unit = "serving" }
        });
        var lookup = new FakeNutritionFactsLookupService();
        var service = new NutritionChatQueryService(parser, lookup);

        var result = await service.SearchAsync("макароны с курицей", CancellationToken.None);

        Assert.Equal(new[] { "pasta", "chicken" }, lookup.Queries);
        Assert.Equal(6, result.Count);
        Assert.Equal(3, result.Count(item => item.ProductName.StartsWith("pasta", StringComparison.Ordinal)));
        Assert.Equal(3, result.Count(item => item.ProductName.StartsWith("chicken", StringComparison.Ordinal)));
    }

    private sealed class FakeChatClient : IChatClient
    {
        private readonly string _response;

        public FakeChatClient(string response)
        {
            _response = response;
        }

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, _response)));
        }

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages,
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

        public Task<IReadOnlyCollection<ProductNutritionDto>> SearchAsync(string query, CancellationToken cancellationToken)
        {
            _queries.Add(query);

            IReadOnlyCollection<ProductNutritionDto> results = Enumerable.Range(1, 5)
                .Select(index => new ProductNutritionDto
                {
                    ProductId = $"{query}-{index}",
                    ProductName = $"{query} {index}",
                    NutritionFacts = new NutritionFactsDto { Calories = index },
                    SourceType = "Test",
                    SourceReference = "Test",
                    ConfidenceScore = 1
                })
                .ToArray();

            return Task.FromResult(results);
        }
    }
}
