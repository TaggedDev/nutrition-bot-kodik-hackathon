namespace Nutrition.Infrastructure.Agent;

internal static class FoodUnitNormalizer
{
    private static readonly string[] BrandHints =
    {
        "creative kitchen",
        "prime cafe",
        "onepricecoffee",
        "one price coffee",
        "самокат",
        "тануки",
        "cofix",
        "кофикс",
        "kfc",
        "кфс",
        "макдональдс",
        "мак",
        "вкусвилл",
        "петелинка",
        "макфа"
    };

    public static FoodUnit Normalize(FoodUnit foodUnit)
    {
        var productName = NormalizeKnownTypos(foodUnit.ProductName.Trim());
        var brand = foodUnit.Brand?.Trim();
        var kind = foodUnit.Kind;

        if (string.IsNullOrWhiteSpace(brand))
        {
            var brandFromName = FindBrandHint(productName);
            if (!string.IsNullOrWhiteSpace(brandFromName))
            {
                brand = brandFromName;
                productName = RemoveBrand(productName, brandFromName);
                kind = IsPreparedBrand(brandFromName) ? FoodUnitKind.PreparedFood : kind;
            }
        }

        if (kind == FoodUnitKind.Unknown && LooksLikePreparedFood(productName, brand))
        {
            kind = FoodUnitKind.PreparedFood;
        }

        return foodUnit with
        {
            ProductName = string.IsNullOrWhiteSpace(productName) ? foodUnit.ProductName.Trim() : productName,
            Brand = string.IsNullOrWhiteSpace(brand) ? null : brand,
            Kind = kind
        };
    }

    private static string NormalizeKnownTypos(string value)
        => value.Replace("оригато", "аригато", StringComparison.OrdinalIgnoreCase);

    private static string? FindBrandHint(string productName)
        => BrandHints.FirstOrDefault(hint => productName.Contains(hint, StringComparison.OrdinalIgnoreCase));

    private static string RemoveBrand(string productName, string brand)
    {
        var index = productName.IndexOf(brand, StringComparison.OrdinalIgnoreCase);
        if (index < 0)
        {
            return productName.Trim();
        }

        return (productName[..index] + productName[(index + brand.Length)..])
            .Replace("  ", " ", StringComparison.Ordinal)
            .Trim(' ', ',', '.', '-', '—');
    }

    private static bool IsPreparedBrand(string brand)
        => brand.Contains("тануки", StringComparison.OrdinalIgnoreCase) ||
           brand.Contains("самокат", StringComparison.OrdinalIgnoreCase) ||
           brand.Contains("prime cafe", StringComparison.OrdinalIgnoreCase) ||
           brand.Contains("creative kitchen", StringComparison.OrdinalIgnoreCase) ||
           brand.Contains("cofix", StringComparison.OrdinalIgnoreCase) ||
           brand.Contains("кофикс", StringComparison.OrdinalIgnoreCase) ||
           brand.Contains("kfc", StringComparison.OrdinalIgnoreCase) ||
           brand.Contains("кфс", StringComparison.OrdinalIgnoreCase) ||
           brand.Contains("макдональдс", StringComparison.OrdinalIgnoreCase) ||
           brand.Equals("мак", StringComparison.OrdinalIgnoreCase);

    private static bool LooksLikePreparedFood(string productName, string? brand)
        => IsPreparedBrand(brand ?? string.Empty) ||
           productName.Contains("сет", StringComparison.OrdinalIgnoreCase) ||
           productName.Contains("ролл", StringComparison.OrdinalIgnoreCase) ||
           productName.Contains("суши", StringComparison.OrdinalIgnoreCase) ||
           productName.Contains("суп", StringComparison.OrdinalIgnoreCase);
}
