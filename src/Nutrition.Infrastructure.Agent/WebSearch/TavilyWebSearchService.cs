using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Nutrition.Infrastructure.Agent.WebSearch;

public sealed class TavilyWebSearchService : IWebSearchService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<TavilyWebSearchService> _logger;
    private readonly TavilyOptions _options;

    public TavilyWebSearchService(
        HttpClient httpClient,
        ILogger<TavilyWebSearchService> logger,
        IOptions<TavilyOptions> options)
    {
        _httpClient = httpClient;
        _logger = logger;
        _options = options.Value;
    }

    public async Task<WebSearchResponse> SearchAsync(WebSearchRequest request, CancellationToken cancellationToken)
    {
        var apiKey = FirstNonEmpty(_options.ApiKey, Environment.GetEnvironmentVariable("TAVILY_API_KEY"));

        if (string.IsNullOrWhiteSpace(request.Query))
        {
            return new WebSearchResponse(Array.Empty<WebSearchResult>(), null, null);
        }

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            _logger.LogWarning("Tavily API key is not configured. Web search will return no results.");
            return new WebSearchResponse(Array.Empty<WebSearchResult>(), null, null);
        }

        _httpClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);

        var payload = new TavilySearchRequest
        {
            Query = request.Query.Trim(),
            Topic = "general",
            SearchDepth = request.Depth == WebSearchDepth.Advanced ? "advanced" : "basic",
            MaxResults = request.MaxResults,
            IncludeAnswer = false,
            IncludeRawContent = false,
            IncludeImages = false,
            AutoParameters = false,
            IncludeUsage = true,
            Country = request.Country
        };

        try
        {
            using var response = await _httpClient.PostAsJsonAsync("search", payload, cancellationToken);
            if (response.StatusCode is HttpStatusCode.TooManyRequests or HttpStatusCode.Unauthorized)
            {
                _logger.LogWarning("Tavily returned non-retryable status {StatusCode}", (int)response.StatusCode);
                return new WebSearchResponse(Array.Empty<WebSearchResult>(), null, null);
            }

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Tavily returned status {StatusCode}", (int)response.StatusCode);
                return new WebSearchResponse(Array.Empty<WebSearchResult>(), null, null);
            }

            var result = await response.Content.ReadFromJsonAsync<TavilySearchResponse>(
                cancellationToken: cancellationToken);

            var mapped = result?.Results?
                .Select(MapResult)
                .OfType<WebSearchResult>()
                .ToArray() ?? Array.Empty<WebSearchResult>();

            return new WebSearchResponse(mapped, result?.RequestId, result?.Usage?.Credits);
        }
        catch (Exception ex) when (ex is TaskCanceledException or HttpRequestException or InvalidOperationException)
        {
            _logger.LogWarning(ex, "Tavily search failed for query: {Query}", request.Query);
            return new WebSearchResponse(Array.Empty<WebSearchResult>(), null, null);
        }
    }

    private static WebSearchResult? MapResult(TavilySearchResult item)
    {
        if (string.IsNullOrWhiteSpace(item.Url) ||
            !Uri.TryCreate(item.Url, UriKind.Absolute, out var uri) ||
            uri.Scheme is not ("http" or "https"))
        {
            return null;
        }

        return new WebSearchResult(
            item.Title ?? string.Empty,
            uri,
            item.Content ?? string.Empty,
            item.Score);
    }

    private static string FirstNonEmpty(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }

        return string.Empty;
    }

    private sealed class TavilySearchRequest
    {
        [JsonPropertyName("query")] public string Query { get; init; } = string.Empty;

        [JsonPropertyName("topic")] public string Topic { get; init; } = "general";

        [JsonPropertyName("search_depth")] public string SearchDepth { get; init; } = "basic";

        [JsonPropertyName("max_results")] public int MaxResults { get; init; } = 5;

        [JsonPropertyName("include_answer")] public bool IncludeAnswer { get; init; }

        [JsonPropertyName("include_raw_content")] public bool IncludeRawContent { get; init; }

        [JsonPropertyName("include_images")] public bool IncludeImages { get; init; }

        [JsonPropertyName("auto_parameters")] public bool AutoParameters { get; init; }

        [JsonPropertyName("include_usage")] public bool IncludeUsage { get; init; }

        [JsonPropertyName("country")] public string? Country { get; init; }
    }

    private sealed class TavilySearchResponse
    {
        [JsonPropertyName("results")] public IReadOnlyCollection<TavilySearchResult>? Results { get; init; }

        [JsonPropertyName("request_id")] public string? RequestId { get; init; }

        [JsonPropertyName("usage")] public TavilyUsage? Usage { get; init; }
    }

    private sealed class TavilySearchResult
    {
        [JsonPropertyName("title")] public string? Title { get; init; }

        [JsonPropertyName("url")] public string? Url { get; init; }

        [JsonPropertyName("content")] public string? Content { get; init; }

        [JsonPropertyName("score")] public decimal Score { get; init; }
    }

    private sealed class TavilyUsage
    {
        [JsonPropertyName("credits")] public int? Credits { get; init; }
    }
}
