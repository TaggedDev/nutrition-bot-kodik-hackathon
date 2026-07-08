using Microsoft.Extensions.AI;
using Nutrition.Infrastructure.Agent;
using Nutrition.Infrastructure.Agent.Parsing;

namespace Nutrition.Application.Tests;

public sealed class MafFoodInputParserTests
{
    [Fact]
    public async Task ParseAsync_ReturnsEmpty_WhenInputIsBlank()
    {
        var parser = new MafFoodInputParser(new FakeChatClient("""{ "items": [] }"""));

        var result = await parser.ParseAsync("   ", CancellationToken.None);

        Assert.Empty(result);
    }

    [Fact]
    public async Task ParseAsync_NormalizesAndFiltersModelOutput()
    {
        const string responseJson = """
        {
          "items": [
            {
              "productName": "  Greek yogurt  ",
              "quantity": 0,
              "unit": "  ",
              "brand": "  Barilla  ",
              "preparation": "  chilled  "
            },
            {
              "productName": "   ",
              "quantity": 2,
              "unit": "g"
            },
            {
              "productName": "Egg",
              "quantity": 2,
              "unit": "pieces"
            }
          ]
        }
        """;

        var parser = new MafFoodInputParser(new FakeChatClient(responseJson));

        var result = await parser.ParseAsync("yogurt and eggs", CancellationToken.None);

        Assert.Equal(2, result.Count);
        var first = result.First();
        Assert.Equal("Greek yogurt", first.ProductName);
        Assert.Equal(1m, first.Quantity);
        Assert.Equal("serving", first.Unit);
        Assert.Equal("Barilla", first.Brand);
        Assert.Equal("chilled", first.Preparation);
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
}
