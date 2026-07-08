using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using Nutrition.Infrastructure.Agent.DeepSeek;

namespace Nutrition.Application.Tests;

public sealed class DeepSeekChatClientTests
{
    [Fact]
    public async Task GetResponseAsync_Throws_WhenApiKeyIsMissing()
    {
        var client = CreateClient(new RecordingHttpMessageHandler(_ => throw new InvalidOperationException("HTTP must not be called.")), apiKey: string.Empty);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.GetResponseAsync(new[] { new ChatMessage(ChatRole.User, "hello") }, cancellationToken: CancellationToken.None));

        Assert.Contains("DEEPSEEK_API_KEY", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetResponseAsync_SerializesMessagesAndReturnsAssistantText()
    {
        var handler = new RecordingHttpMessageHandler(request =>
        {
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.EndsWith("/chat/completions", request.RequestUri!.AbsoluteUri, StringComparison.OrdinalIgnoreCase);
            Assert.Equal("Bearer test-key", request.Headers.Authorization!.ToString());

            var json = JsonDocument.Parse(request.Content!.ReadAsStringAsync().Result);
            Assert.Equal("deepseek-chat", json.RootElement.GetProperty("model").GetString());

            var messages = json.RootElement.GetProperty("messages");
            Assert.Equal(2, messages.GetArrayLength());
            Assert.Equal("system", messages[0].GetProperty("role").GetString());
            Assert.Equal("user", messages[1].GetProperty("role").GetString());
            Assert.Equal("hello", messages[1].GetProperty("content").GetString());

            var response = """
            {
              "id": "resp-1",
              "model": "deepseek-chat",
              "choices": [
                {
                  "message": {
                    "content": "parsed answer"
                  }
                }
              ]
            }
            """;

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(response, Encoding.UTF8, "application/json")
            };
        });

        var client = CreateClient(handler);

        var response = await client.GetResponseAsync(
            new[] { new ChatMessage(ChatRole.User, "hello") },
            new ChatOptions
            {
                Instructions = "system prompt",
                ModelId = "deepseek-chat",
                Temperature = 0.2f,
                MaxOutputTokens = 128,
                ResponseFormat = ChatResponseFormat.Json
            },
            CancellationToken.None);

        Assert.Equal("parsed answer", response.Text);
        Assert.Equal("resp-1", response.ResponseId);
        Assert.Equal("deepseek-chat", response.ModelId);
        Assert.Equal(1, handler.CallsCount);
    }

    [Fact]
    public async Task GetStreamingResponseAsync_DelegatesToSingleResponse()
    {
        var handler = new RecordingHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""
            {
              "choices": [
                {
                  "message": {
                    "content": "streamed text"
                  }
                }
              ]
            }
            """, Encoding.UTF8, "application/json")
        });

        var client = CreateClient(handler);

        var updates = new List<ChatResponseUpdate>();
        await foreach (var update in client.GetStreamingResponseAsync(new[] { new ChatMessage(ChatRole.User, "hello") }))
        {
            updates.Add(update);
        }

        var single = Assert.Single(updates);
        Assert.Equal("streamed text", single.Text);
    }

    private static DeepSeekChatClient CreateClient(RecordingHttpMessageHandler handler, string apiKey = "test-key")
    {
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.deepseek.com/")
        };

        return new DeepSeekChatClient(httpClient, Options.Create(new DeepSeekOptions
        {
            ApiKey = apiKey,
            BaseUrl = "https://api.deepseek.com",
            Model = "deepseek-chat",
            TimeoutSeconds = 30
        }));
    }

    private sealed class RecordingHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responseFactory;

        public RecordingHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory)
        {
            _responseFactory = responseFactory;
        }

        public int CallsCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CallsCount++;
            return Task.FromResult(_responseFactory(request));
        }
    }
}
