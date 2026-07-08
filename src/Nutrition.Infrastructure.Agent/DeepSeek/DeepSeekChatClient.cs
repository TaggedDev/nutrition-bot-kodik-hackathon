using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;

namespace Nutrition.Infrastructure.Agent.DeepSeek;

public sealed class DeepSeekChatClient : IChatClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly HttpClient _httpClient;
    private readonly DeepSeekOptions _options;

    public DeepSeekChatClient(HttpClient httpClient, IOptions<DeepSeekOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
    }

    public async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            throw new InvalidOperationException("DEEPSEEK_API_KEY is not configured.");
        }

        var outboundMessages = new List<DeepSeekMessage>();
        if (!string.IsNullOrWhiteSpace(options?.Instructions))
        {
            outboundMessages.Add(new DeepSeekMessage("system", options.Instructions));
        }

        outboundMessages.AddRange(messages.Select(ToDeepSeekMessage));

        var request = new DeepSeekChatRequest
        {
            Model = options?.ModelId ?? _options.Model,
            Messages = outboundMessages,
            Temperature = options?.Temperature,
            MaxTokens = options?.MaxOutputTokens,
            ResponseFormat = BuildResponseFormat(options?.ResponseFormat)
        };

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "chat/completions")
        {
            Content = new StringContent(JsonSerializer.Serialize(request, JsonOptions), Encoding.UTF8, "application/json")
        };
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);

        using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var payload = await JsonSerializer.DeserializeAsync<DeepSeekChatResponse>(stream, JsonOptions, cancellationToken);
        var text = payload?.Choices?.FirstOrDefault()?.Message?.Content ?? string.Empty;

        return new ChatResponse(new ChatMessage(ChatRole.Assistant, text))
        {
            ModelId = payload?.Model ?? request.Model,
            ResponseId = payload?.Id
        };
    }

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var response = await GetResponseAsync(messages, options, cancellationToken);
        yield return new ChatResponseUpdate(ChatRole.Assistant, response.Text)
        {
            ResponseId = response.ResponseId,
            ModelId = response.ModelId
        };
    }

    public object? GetService(Type serviceType, object? serviceKey = null)
    {
        return serviceType.IsInstanceOfType(this) ? this : null;
    }

    public void Dispose()
    {
    }

    private static DeepSeekMessage ToDeepSeekMessage(ChatMessage message)
    {
        var role = message.Role == ChatRole.System ? "system"
            : message.Role == ChatRole.Assistant ? "assistant"
            : message.Role == ChatRole.Tool ? "tool"
            : "user";

        return new DeepSeekMessage(role, message.Text);
    }

    private static object? BuildResponseFormat(ChatResponseFormat? responseFormat)
    {
        if (responseFormat is null || responseFormat == ChatResponseFormat.Text)
        {
            return null;
        }

        return responseFormat == ChatResponseFormat.Json
            ? new { type = "json_object" }
            : new { type = "json_object" };
    }

    private sealed class DeepSeekChatRequest
    {
        [JsonPropertyName("model")]
        public string Model { get; init; } = string.Empty;

        [JsonPropertyName("messages")]
        public IReadOnlyCollection<DeepSeekMessage> Messages { get; init; } = Array.Empty<DeepSeekMessage>();

        [JsonPropertyName("temperature")]
        public double? Temperature { get; init; }

        [JsonPropertyName("max_tokens")]
        public int? MaxTokens { get; init; }

        [JsonPropertyName("response_format")]
        public object? ResponseFormat { get; init; }
    }

    private sealed record DeepSeekMessage(
        [property: JsonPropertyName("role")] string Role,
        [property: JsonPropertyName("content")] string Content);

    private sealed class DeepSeekChatResponse
    {
        [JsonPropertyName("id")]
        public string? Id { get; init; }

        [JsonPropertyName("model")]
        public string? Model { get; init; }

        [JsonPropertyName("choices")]
        public List<DeepSeekChoice>? Choices { get; init; }
    }

    private sealed class DeepSeekChoice
    {
        [JsonPropertyName("message")]
        public DeepSeekResponseMessage? Message { get; init; }
    }

    private sealed class DeepSeekResponseMessage
    {
        [JsonPropertyName("content")]
        public string? Content { get; init; }
    }
}
