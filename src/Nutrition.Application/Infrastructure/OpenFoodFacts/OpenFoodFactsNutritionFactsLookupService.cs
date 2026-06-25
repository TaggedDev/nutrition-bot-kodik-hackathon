using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Nutrition.Application.Abstractions.Services;
using Nutrition.Shared.Dtos;

namespace Nutrition.Application.Infrastructure.OpenFoodFacts;

public sealed class OpenFoodFactsNutritionFactsLookupService : INutritionFactsLookupService
{
    private const int PageSize = 15;
    private readonly HttpClient _httpClient;

    public OpenFoodFactsNutritionFactsLookupService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<IReadOnlyCollection<ProductNutritionDto>> SearchAsync(string query, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return Array.Empty<ProductNutritionDto>();
        }

        var escapedQuery = Uri.EscapeDataString(query.Trim());
        var requestUri = $"/cgi/search.pl?search_terms={escapedQuery}&search_simple=1&action=process&json=1&page_size={PageSize}";

        using var response = await _httpClient.GetAsync(requestUri, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return Array.Empty<ProductNutritionDto>();
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var payload = await JsonSerializer.DeserializeAsync<OpenFoodFactsSearchResponse>(stream, cancellationToken: cancellationToken);

        if (payload?.Products is null || payload.Products.Count == 0)
        {
            return Array.Empty<ProductNutritionDto>();
        }

        var result = new List<ProductNutritionDto>(payload.Products.Count);

        foreach (var product in payload.Products)
        {
            if (product?.Nutriments.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            if (!TryBuildNutritionFacts(product.Nutriments, out var nutritionFacts))
            {
                continue;
            }

            var productName = FirstNonEmpty(product.ProductName, product.ProductNameEn, "Unnamed product");
            var code = product.Code?.Trim();

            result.Add(new ProductNutritionDto
            {
                ProductId = string.IsNullOrWhiteSpace(code) ? Guid.NewGuid().ToString("N") : code,
                ProductName = productName,
                Brand = string.IsNullOrWhiteSpace(product.Brands) ? null : product.Brands.Trim(),
                NutritionFacts = nutritionFacts,
                SourceType = "OpenFoodFacts",
                SourceReference = string.IsNullOrWhiteSpace(code)
                    ? $"OFF:search:{query.Trim()}"
                    : $"OFF:{code}",
                ConfidenceScore = BuildConfidenceScore(product)
            });
        }

        return result;
    }

    private static bool TryBuildNutritionFacts(JsonElement nutriments, out NutritionFactsDto nutritionFacts)
    {
        nutritionFacts = new NutritionFactsDto();

        var hasCalories = TryGetDecimalFromAny(
            nutriments,
            out var calories,
            "energy-kcal_100g",
            "energy-kcal",
            "energy-kcal_serving");

        if (!hasCalories
            && TryGetDecimalFromAny(nutriments, out var energyKj, "energy-kj_100g", "energy-kj", "energy_100g", "energy"))
        {
            calories = decimal.Round(energyKj / 4.184m, 2, MidpointRounding.AwayFromZero);
            hasCalories = true;
        }

        var hasProtein = TryGetDecimalFromAny(nutriments, out var protein, "proteins_100g", "proteins", "proteins_serving");
        var hasFat = TryGetDecimalFromAny(nutriments, out var fat, "fat_100g", "fat", "fat_serving");
        var hasCarbs = TryGetDecimalFromAny(nutriments, out var carbs, "carbohydrates_100g", "carbohydrates", "carbohydrates_serving");

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
                return decimal.TryParse(
                    normalized,
                    NumberStyles.Any,
                    CultureInfo.InvariantCulture,
                    out value);
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

    private sealed class OpenFoodFactsSearchResponse
    {
        [JsonPropertyName("products")]
        public List<OpenFoodFactsProduct>? Products { get; init; }
    }

    private sealed class OpenFoodFactsProduct
    {
        [JsonPropertyName("code")]
        public string? Code { get; init; }

        [JsonPropertyName("product_name")]
        public string? ProductName { get; init; }

        [JsonPropertyName("product_name_en")]
        public string? ProductNameEn { get; init; }

        [JsonPropertyName("brands")]
        public string? Brands { get; init; }

        [JsonPropertyName("nutriments")]
        public JsonElement Nutriments { get; init; }
    }
}
