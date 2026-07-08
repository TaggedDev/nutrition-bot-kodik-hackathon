using Nutrition.Core.Domain.Enums;
using Nutrition.Core.Domain.Exceptions;

namespace Nutrition.Core.Domain.Entities;

public sealed class UserMealEntry
{
    public UserMealEntry(Guid id, Guid userId, string productName, string brand, decimal calories, decimal protein, decimal fat, decimal carbs, MealType mealType, DateTimeOffset createdAtUtc)
    {
        if (id == Guid.Empty)
            throw new DomainValidationException("Entry id is required.");
        if (userId == Guid.Empty)
            throw new DomainValidationException("User id is required.");
        if (string.IsNullOrWhiteSpace(productName))
            throw new DomainValidationException("Product name is required.");
        if (calories < 0 || protein < 0 || fat < 0 || carbs < 0)
            throw new DomainValidationException("Nutrition values cannot be negative.");

        Id = id;
        UserId = userId;
        ProductName = productName.Trim();
        Brand = brand?.Trim() ?? string.Empty;
        Calories = calories;
        Protein = protein;
        Fat = fat;
        Carbs = carbs;
        MealType = mealType;
        CreatedAtUtc = createdAtUtc;
        UpdatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; }

    public Guid UserId { get; }

    public string ProductName { get; private set; }

    public string Brand { get; private set; }

    public decimal Calories { get; private set; }

    public decimal Protein { get; private set; }

    public decimal Fat { get; private set; }

    public decimal Carbs { get; private set; }

    public MealType MealType { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public void Update(string productName, string brand, decimal calories, decimal protein, decimal fat, decimal carbs, MealType mealType)
    {
        if (string.IsNullOrWhiteSpace(productName))
            throw new DomainValidationException("Product name is required.");
        if (calories < 0 || protein < 0 || fat < 0 || carbs < 0)
            throw new DomainValidationException("Nutrition values cannot be negative.");

        ProductName = productName.Trim();
        Brand = brand?.Trim() ?? string.Empty;
        Calories = calories;
        Protein = protein;
        Fat = fat;
        Carbs = carbs;
        MealType = mealType;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
    }
}
