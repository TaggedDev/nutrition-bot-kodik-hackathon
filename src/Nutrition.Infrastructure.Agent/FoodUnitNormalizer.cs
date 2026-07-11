namespace Nutrition.Infrastructure.Agent;

internal static class FoodUnitNormalizer
{
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
    
    private static string RemoveBrand(string productName, string brand)
    {
        var index = productName.IndexOf(brand, StringComparison.OrdinalIgnoreCase);
        if (index < 0)
        {
            return productName.Trim();
        }

        return (productName[..index] + productName[(index + brand.Length)..])
            .Replace("  ", " ", StringComparison.Ordinal).Trim(' ', ',', '.', '-', '—');
    }
}