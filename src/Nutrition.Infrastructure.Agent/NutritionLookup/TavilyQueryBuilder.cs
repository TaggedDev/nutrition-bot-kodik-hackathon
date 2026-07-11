namespace Nutrition.Infrastructure.Agent.NutritionLookup;

public sealed class TavilyQueryBuilder : ITavilyQueryBuilder
{
    public string Build(FoodUnit foodUnit)
    {
        var parts = new List<string>();

        if (!string.IsNullOrWhiteSpace(foodUnit.Brand))
        {
            parts.Add(foodUnit.Brand.Trim());
        }

        parts.Add(foodUnit.ProductName.Trim());
        var productQuery = string.Join(' ', parts);
        if (IsBeverage(productQuery))
        {
            return
                $"{productQuery} кбжу пищевая ценность на 100 мл или порцию точные значения ккал белки жиры углеводы объем";
        }

        if (foodUnit.Kind == FoodUnitKind.PreparedFood || ContainsCyrillic(productQuery))
        {
            return $"{productQuery} кбжу пищевая ценность на 100 г точные значения ккал белки жиры углеводы";
        }

        return $"{productQuery} nutrition calories protein fat carbs carbohydrates serving weight";
    }

    private static bool ContainsCyrillic(string value)
        => value.Any(ch => ch is >= '\u0400' and <= '\u04FF');

    private static bool IsBeverage(string value)
    {
        var normalized = value.ToLowerInvariant();
        return new[] { "кофе", "латте", "капучино", "американо", "эспрессо", "чай", "напиток", "сок", "смузи" }.Any(
            normalized.Contains);
    }
}