using Nutrition.Core.Domain.Exceptions;
using Nutrition.Core.Domain.ValueObjects;

namespace Nutrition.Core.Domain.Entities;

public sealed class FoodProduct
{
    public FoodProduct(
        Guid id,
        string name,
        NutritionFacts nutritionPer100g,
        string? brand,
        string? barcode)
    {
        if (id == Guid.Empty)
        {
            throw new DomainValidationException("Product id is required.");
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainValidationException("Product name is required.");
        }

        Id = id;
        Name = name.Trim();
        NutritionPer100g = nutritionPer100g;
        Brand = string.IsNullOrWhiteSpace(brand) ? null : brand.Trim();
        Barcode = string.IsNullOrWhiteSpace(barcode) ? null : barcode.Trim();
    }

    public Guid Id { get; }

    public string Name { get; }

    public string? Brand { get; }

    public string? Barcode { get; }

    public NutritionFacts NutritionPer100g { get; }
}
