using Nutrition.Core.Domain.Enums;
using Nutrition.Core.Domain.Exceptions;

namespace Nutrition.Core.Domain.Entities;

public sealed class UserMealEntry
{
    private const string DefaultPortionLabel = "Порция не указана";

    public UserMealEntry(Guid id, Guid userId, string productName, string brand, decimal calories, decimal protein, decimal fat, decimal carbs, MealType mealType, DateTimeOffset createdAtUtc)
        : this(id, userId, productName, brand, calories, protein, fat, carbs, mealType, 0, DefaultPortionLabel, string.Empty, string.Empty, createdAtUtc, createdAtUtc)
    {
    }

    public UserMealEntry(Guid id, Guid userId, string productName, string brand, decimal calories, decimal protein,
        decimal fat, decimal carbs, MealType mealType, decimal servingGrams, string? portionLabel, string? sourceType,
        string? sourceReference, DateTimeOffset loggedAtUtc, DateTimeOffset createdAtUtc)
    {
        if (id == Guid.Empty)
            throw new DomainValidationException("Entry id is required.");
        if (userId == Guid.Empty)
            throw new DomainValidationException("User id is required.");
        if (string.IsNullOrWhiteSpace(productName))
            throw new DomainValidationException("Product name is required.");
        if (calories < 0 || protein < 0 || fat < 0 || carbs < 0)
            throw new DomainValidationException("Nutrition values cannot be negative.");
        if (servingGrams < 0)
            throw new DomainValidationException("Serving grams cannot be negative.");

        Id = id;
        UserId = userId;
        ProductName = productName.Trim();
        Brand = brand?.Trim() ?? string.Empty;
        Calories = calories;
        Protein = protein;
        Fat = fat;
        Carbs = carbs;
        MealType = mealType;
        ServingGrams = servingGrams;
        PortionLabel = string.IsNullOrWhiteSpace(portionLabel) ? DefaultPortionLabel : portionLabel.Trim();
        SourceType = sourceType?.Trim() ?? string.Empty;
        SourceReference = sourceReference?.Trim() ?? string.Empty;
        LoggedAtUtc = loggedAtUtc;
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

    public decimal ServingGrams { get; private set; }

    public string PortionLabel { get; private set; }

    public string SourceType { get; private set; }

    public string SourceReference { get; private set; }

    public DateTimeOffset LoggedAtUtc { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public void Update(string productName, string brand, decimal calories, decimal protein, decimal fat, decimal carbs, MealType mealType)
    {
        Update(productName, brand, calories, protein, fat, carbs, mealType, ServingGrams, PortionLabel, SourceType, SourceReference, LoggedAtUtc);
    }

    public void Update(string productName, string brand, decimal calories, decimal protein, decimal fat, decimal carbs,
        MealType mealType, decimal servingGrams, string? portionLabel, string? sourceType, string? sourceReference,
        DateTimeOffset loggedAtUtc)
    {
        if (string.IsNullOrWhiteSpace(productName))
            throw new DomainValidationException("Product name is required.");
        if (calories < 0 || protein < 0 || fat < 0 || carbs < 0)
            throw new DomainValidationException("Nutrition values cannot be negative.");
        if (servingGrams < 0)
            throw new DomainValidationException("Serving grams cannot be negative.");

        ProductName = productName.Trim();
        Brand = brand?.Trim() ?? string.Empty;
        Calories = calories;
        Protein = protein;
        Fat = fat;
        Carbs = carbs;
        MealType = mealType;
        ServingGrams = servingGrams;
        PortionLabel = string.IsNullOrWhiteSpace(portionLabel) ? DefaultPortionLabel : portionLabel.Trim();
        SourceType = sourceType?.Trim() ?? string.Empty;
        SourceReference = sourceReference?.Trim() ?? string.Empty;
        LoggedAtUtc = loggedAtUtc;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
    }
}
