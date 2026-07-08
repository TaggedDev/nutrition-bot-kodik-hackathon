using Nutrition.Core.Domain.Exceptions;

namespace Nutrition.Core.Domain.Entities;

public sealed class UserFoodPreference
{
    private readonly HashSet<string> _allergens = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _dislikedFoods = new(StringComparer.OrdinalIgnoreCase);

    public UserFoodPreference(Guid userId, bool isVegetarian, bool isVegan)
    {
        if (userId == Guid.Empty)
        {
            throw new DomainValidationException("User id is required.");
        }

        UserId = userId;
        IsVegetarian = isVegetarian;
        IsVegan = isVegan;
    }

    public Guid UserId { get; }

    public bool IsVegetarian { get; }

    public bool IsVegan { get; }

    public IReadOnlyCollection<string> Allergens => _allergens;

    public IReadOnlyCollection<string> DislikedFoods => _dislikedFoods;

    public void AddAllergen(string allergen)
    {
        if (!string.IsNullOrWhiteSpace(allergen))
        {
            _allergens.Add(allergen.Trim());
        }
    }

    public void AddDislikedFood(string productName)
    {
        if (!string.IsNullOrWhiteSpace(productName))
        {
            _dislikedFoods.Add(productName.Trim());
        }
    }
}