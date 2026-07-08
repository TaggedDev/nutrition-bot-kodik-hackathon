using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nutrition.Application.Abstractions.Services;
using Nutrition.Shared.Dtos;

namespace Nutrition.Application.Infrastructure.OpenFoodFacts;

public sealed class OpenFoodFactsNutritionFactsLookupService : INutritionFactsLookupService
{
    private const string ProductFields = "code,product_name,product_name_en,brands,nutriments";
    private readonly HttpClient _httpClient;
    private readonly IMemoryCache _cache;
    private readonly ILogger<OpenFoodFactsNutritionFactsLookupService> _logger;
    private readonly IOpenFoodFactsRateLimiter _rateLimiter;
    private readonly OpenFoodFactsOptions _options;

    public OpenFoodFactsNutritionFactsLookupService(HttpClient httpClient, IMemoryCache cache,
        ILogger<OpenFoodFactsNutritionFactsLookupService> logger, IOpenFoodFactsRateLimiter rateLimiter,
        IOptions<OpenFoodFactsOptions> options)
    {
        _httpClient = httpClient;
        _cache = cache;
        _logger = logger;
        _rateLimiter = rateLimiter;
        _options = options.Value;
    }

    public async Task<IReadOnlyCollection<ProductNutritionDto>> SearchAsync(string query,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return Array.Empty<ProductNutritionDto>();
        }

        var normalizedQuery = query.Trim();

        if (IsBarcode(normalizedQuery))
        {
            var barcodeCacheKey = BuildBarcodeCacheKey(normalizedQuery);
            if (_cache.TryGetValue<IReadOnlyCollection<ProductNutritionDto>>(barcodeCacheKey,
                    out var cachedBarcodeResult))
            {
                return cachedBarcodeResult ?? Array.Empty<ProductNutritionDto>();
            }

            var barcodeLookup = await LookupByBarcodeAsync(normalizedQuery, cancellationToken);
            if (barcodeLookup.Cacheable)
            {
                SetCache(barcodeCacheKey, barcodeLookup.Result);
            }

            return barcodeLookup.Result;
        }

        var searchCacheKey = BuildSearchCacheKey(normalizedQuery);
        if (_cache.TryGetValue<IReadOnlyCollection<ProductNutritionDto>>(searchCacheKey, out var cachedSearchResult))
        {
            return cachedSearchResult ?? Array.Empty<ProductNutritionDto>();
        }

        if (!_rateLimiter.TryAcquireSearchSlot())
        {
            _logger.LogWarning("OpenFoodFacts text search throttled for query: {Query}", normalizedQuery);
            return Array.Empty<ProductNutritionDto>();
        }

        var textLookup = await LookupByTextV2Async(normalizedQuery, cancellationToken);

        if (!textLookup.Success && _options.EnableLegacyCgiFallback)
        {
            textLookup = await LookupByLegacyCgiFallbackAsync(normalizedQuery, cancellationToken);
        }

        if (textLookup.Cacheable)
        {
            SetCache(searchCacheKey, textLookup.Result);
        }

        return textLookup.Result;
    }

    private async Task<LookupResult> LookupByBarcodeAsync(string barcode, CancellationToken cancellationToken)
    {
        var requestUri = $"/api/v2/product/{Uri.EscapeDataString(barcode)}.json?fields={ProductFields}";
        try
        {
            using var response = await _httpClient.GetAsync(requestUri, cancellationToken);
            if (response.StatusCode == HttpStatusCode.ServiceUnavailable)
            {
                _logger.LogWarning("OpenFoodFacts returned 503 for barcode query: {Barcode}", barcode);
                return LookupResult.NonCacheableEmpty;
            }

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("OpenFoodFacts non-success status {StatusCode} for barcode query: {Barcode}",
                    (int)response.StatusCode, barcode);
                return LookupResult.CacheableEmpty;
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var payload =
                await JsonSerializer.DeserializeAsync<OpenFoodFactsProductResponse>(stream,
                    cancellationToken: cancellationToken);

            if (payload?.Product is null)
            {
                return LookupResult.CacheableEmpty;
            }

            var mapped = MapProducts(new[] { payload.Product }, barcode);
            return new LookupResult(mapped, true, true);
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("OpenFoodFacts timeout for barcode query: {Barcode}", barcode);
            return LookupResult.NonCacheableEmpty;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "OpenFoodFacts HTTP error for barcode query: {Barcode}", barcode);
            return LookupResult.NonCacheableEmpty;
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "OpenFoodFacts JSON parse error for barcode query: {Barcode}", barcode);
            return LookupResult.NonCacheableEmpty;
        }
    }

    private async Task<LookupResult> LookupByTextV2Async(string query, CancellationToken cancellationToken)
    {
        var escapedQuery = Uri.EscapeDataString(query);
        var requestUri =
            $"{_options.SearchBaseUrl.TrimEnd('/')}/search?q={escapedQuery}&fields={ProductFields}&page_size={_options.SearchPageSize}";

        try
        {
            using var response = await _httpClient.GetAsync(requestUri, cancellationToken);
            if (response.StatusCode == HttpStatusCode.ServiceUnavailable)
            {
                _logger.LogWarning("OpenFoodFacts search returned 503 for text query: {Query}", query);
                return LookupResult.NonCacheableFailed;
            }

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("OpenFoodFacts search non-success status {StatusCode} for text query: {Query}",
                    (int)response.StatusCode, query);
                return LookupResult.CacheableFailed;
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var payload =
                await JsonSerializer.DeserializeAsync<SearchALiciousResponse>(stream,
                    cancellationToken: cancellationToken);
            var mapped = MapProducts(payload?.Hits ?? new List<OpenFoodFactsProduct>(), query);

            return new LookupResult(mapped, true, true);
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("OpenFoodFacts search timeout for text query: {Query}", query);
            return LookupResult.NonCacheableFailed;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "OpenFoodFacts search HTTP error for text query: {Query}", query);
            return LookupResult.NonCacheableFailed;
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "OpenFoodFacts search JSON parse error for text query: {Query}", query);
            return LookupResult.NonCacheableFailed;
        }
    }

    private async Task<LookupResult> LookupByLegacyCgiFallbackAsync(string query, CancellationToken cancellationToken)
    {
        var escapedQuery = Uri.EscapeDataString(query);
        var requestUri =
            $"/cgi/search.pl?search_terms={escapedQuery}&search_simple=1&action=process&json=1&page_size={_options.SearchPageSize}";

        try
        {
            using var response = await _httpClient.GetAsync(requestUri, cancellationToken);
            if (response.StatusCode == HttpStatusCode.ServiceUnavailable)
            {
                _logger.LogWarning("OpenFoodFacts legacy fallback returned 503 for text query: {Query}", query);
                return LookupResult.NonCacheableEmpty;
            }

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "OpenFoodFacts legacy fallback non-success status {StatusCode} for text query: {Query}",
                    (int)response.StatusCode, query);
                return LookupResult.CacheableEmpty;
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var payload =
                await JsonSerializer.DeserializeAsync<OpenFoodFactsSearchResponse>(stream,
                    cancellationToken: cancellationToken);
            var mapped = MapProducts(payload?.Products ?? new List<OpenFoodFactsProduct>(), query);
            return new LookupResult(mapped, true, true);
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("OpenFoodFacts legacy fallback timeout for text query: {Query}", query);
            return LookupResult.NonCacheableEmpty;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "OpenFoodFacts legacy fallback HTTP error for text query: {Query}", query);
            return LookupResult.NonCacheableEmpty;
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "OpenFoodFacts legacy fallback JSON parse error for text query: {Query}", query);
            return LookupResult.NonCacheableEmpty;
        }
    }

    private IReadOnlyCollection<ProductNutritionDto> MapProducts(IEnumerable<OpenFoodFactsProduct> products,
        string sourceQuery)
    {
        var result = new List<ProductNutritionDto>();

        foreach (var product in products)
        {
            if (product?.Nutriments.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            if (!TryBuildNutritionFacts(product.Nutriments, out var nutritionFacts))
            {
                continue;
            }

            var code = product.Code?.Trim();
            var productName = FirstNonEmpty(product.ProductName, product.ProductNameEn, "Unnamed product");

            result.Add(new ProductNutritionDto
            {
                ProductId = string.IsNullOrWhiteSpace(code) ? Guid.NewGuid().ToString("N") : code,
                ProductName = productName,
                Brand = string.IsNullOrWhiteSpace(product.Brands) ? null : product.Brands.Trim(),
                NutritionFacts = nutritionFacts,
                SourceType = "OpenFoodFacts",
                SourceReference = string.IsNullOrWhiteSpace(code) ? $"OFF:search:{sourceQuery}" : $"OFF:{code}",
                ConfidenceScore = BuildConfidenceScore(product)
            });
        }

        return result;
    }

    private void SetCache(string key, IReadOnlyCollection<ProductNutritionDto> result)
    {
        var ttlHours = Math.Clamp(_options.CacheTtlHours, 6, 24);
        _cache.Set(key, result, TimeSpan.FromHours(ttlHours));
    }

    private static string BuildBarcodeCacheKey(string barcode)
        => $"off:barcode:{barcode}";

    private static string BuildSearchCacheKey(string query)
        => $"off:search:sal:{query.Trim().ToLowerInvariant()}";

    private static bool IsBarcode(string query)
    {
        if (query.Length is not (8 or 12 or 13 or 14))
        {
            return false;
        }

        foreach (var ch in query)
        {
            if (!char.IsDigit(ch))
            {
                return false;
            }
        }

        return true;
    }

    private static bool TryBuildNutritionFacts(JsonElement nutriments, out NutritionFactsDto nutritionFacts)
    {
        nutritionFacts = new NutritionFactsDto();

        var hasCalories = TryGetDecimalFromAny(nutriments, out var calories, "energy-kcal_100g", "energy-kcal",
            "energy-kcal_serving");

        if (!hasCalories && TryGetDecimalFromAny(nutriments, out var energyKj, "energy-kj_100g", "energy-kj",
                "energy_100g", "energy"))
        {
            calories = decimal.Round(energyKj / 4.184m, 2, MidpointRounding.AwayFromZero);
            hasCalories = true;
        }

        var hasProtein =
            TryGetDecimalFromAny(nutriments, out var protein, "proteins_100g", "proteins", "proteins_serving");
        var hasFat = TryGetDecimalFromAny(nutriments, out var fat, "fat_100g", "fat", "fat_serving");
        var hasCarbs = TryGetDecimalFromAny(nutriments, out var carbs, "carbohydrates_100g", "carbohydrates",
            "carbohydrates_serving");

        if (!hasCalories)
        {
            return false;
        }

        nutritionFacts = new NutritionFactsDto
        {
            Calories = calories,
            Protein = hasProtein ? protein : 0m,
            Fat = hasFat ? fat : 0m,
            Carbs = hasCarbs ? carbs : 0m
        };

        return true;
    }

    private static bool TryGetDecimalFromAny(JsonElement element, out decimal value, params string[] propertyNames)
    {
        value = default;

        foreach (var propertyName in propertyNames)
        {
            if (TryGetDecimal(element, propertyName, out value))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryGetDecimal(JsonElement element, string propertyName, out decimal value)
    {
        value = default;

        if (!element.TryGetProperty(propertyName, out var property))
        {
            return false;
        }

        switch (property.ValueKind)
        {
            case JsonValueKind.Number:
                return property.TryGetDecimal(out value);
            case JsonValueKind.String:
                var raw = property.GetString();
                if (string.IsNullOrWhiteSpace(raw))
                {
                    return false;
                }

                var normalized = raw.Replace(',', '.');
                return decimal.TryParse(normalized, NumberStyles.Any, CultureInfo.InvariantCulture, out value);
            default:
                return false;
        }
    }

    private static decimal BuildConfidenceScore(OpenFoodFactsProduct product)
    {
        var score = 0.70m;

        if (!string.IsNullOrWhiteSpace(product.ProductName) || !string.IsNullOrWhiteSpace(product.ProductNameEn))
        {
            score += 0.10m;
        }

        if (!string.IsNullOrWhiteSpace(product.Brands))
        {
            score += 0.10m;
        }

        if (!string.IsNullOrWhiteSpace(product.Code))
        {
            score += 0.10m;
        }

        return Math.Min(score, 1.00m);
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

    private sealed record LookupResult(IReadOnlyCollection<ProductNutritionDto> Result, bool Cacheable, bool Success)
    {
        public static LookupResult CacheableEmpty => new(Array.Empty<ProductNutritionDto>(), true, false);

        public static LookupResult NonCacheableEmpty => new(Array.Empty<ProductNutritionDto>(), false, false);

        public static LookupResult CacheableFailed => new(Array.Empty<ProductNutritionDto>(), true, false);

        public static LookupResult NonCacheableFailed => new(Array.Empty<ProductNutritionDto>(), false, false);
    }

    private sealed class OpenFoodFactsProductResponse
    {
        [JsonPropertyName("product")] public OpenFoodFactsProduct? Product { get; init; }
    }

    private sealed class OpenFoodFactsSearchResponse
    {
        [JsonPropertyName("products")] public List<OpenFoodFactsProduct>? Products { get; init; }
    }

    private sealed class SearchALiciousResponse
    {
        [JsonPropertyName("hits")] public List<OpenFoodFactsProduct>? Hits { get; init; }
    }

    private sealed class OpenFoodFactsProduct
    {
        [JsonPropertyName("code")] public string? Code { get; init; }

        [JsonPropertyName("product_name")] public string? ProductName { get; init; }

        [JsonPropertyName("product_name_en")] public string? ProductNameEn { get; init; }

        [JsonPropertyName("brands")]
        [JsonConverter(typeof(StringOrStringArrayJsonConverter))]
        public string? Brands { get; init; }

        [JsonPropertyName("nutriments")] public JsonElement Nutriments { get; init; }
    }

    private sealed class StringOrStringArrayJsonConverter : JsonConverter<string?>
    {
        public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            return reader.TokenType switch
            {
                JsonTokenType.String => reader.GetString(),
                JsonTokenType.Null => null,
                JsonTokenType.StartArray => ReadStringArray(ref reader),
                _ => SkipUnexpectedValue(ref reader)
            };
        }

        public override void Write(Utf8JsonWriter writer, string? value, JsonSerializerOptions options)
        {
            if (value is null)
            {
                writer.WriteNullValue();
                return;
            }

            writer.WriteStringValue(value);
        }

        private static string? ReadStringArray(ref Utf8JsonReader reader)
        {
            var values = new List<string>();

            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndArray)
                {
                    return values.Count == 0 ? null : string.Join(", ", values);
                }

                if (reader.TokenType == JsonTokenType.String)
                {
                    var value = reader.GetString();
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        values.Add(value.Trim());
                    }

                    continue;
                }

                reader.Skip();
            }

            throw new JsonException("Unexpected end of brands array.");
        }

        private static string? SkipUnexpectedValue(ref Utf8JsonReader reader)
        {
            reader.Skip();
            return null;
        }
    }
}