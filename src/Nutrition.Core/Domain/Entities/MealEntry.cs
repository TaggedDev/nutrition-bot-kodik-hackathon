using Nutrition.Core.Domain.Enums;
using Nutrition.Core.Domain.Exceptions;
using Nutrition.Core.Domain.ValueObjects;

namespace Nutrition.Core.Domain.Entities;

public sealed class MealEntry
{
    private readonly List<MealItem> _items = new();

    public MealEntry(
        Guid id,
        Guid userId,
        MealType mealType,
        DateTimeOffset loggedAtUtc,
        InputChannel inputChannel)
    {
        if (id == Guid.Empty)
        {
            throw new DomainValidationException("Meal id is required.");
        }

        if (userId == Guid.Empty)
        {
            throw new DomainValidationException("User id is required.");
        }

        Id = id;
        UserId = userId;
        MealType = mealType;
        LoggedAtUtc = loggedAtUtc;
        InputChannel = inputChannel;
    }

    public Guid Id { get; }

    public Guid UserId { get; }

    public MealType MealType { get; }

    public DateTimeOffset LoggedAtUtc { get; }

    public InputChannel InputChannel { get; }

    public IReadOnlyCollection<MealItem> Items => _items.AsReadOnly();

    public NutritionFacts TotalNutrition => _items
        .Select(item => item.Nutrition)
        .Aggregate(NutritionFacts.Zero, (current, value) => current.Add(value));

    public bool RequiresUserConfirmation => _items.Any(item => item.RequiresConfirmation);

    public void AddItem(MealItem item)
    {
        _items.Add(item);
    }

    public void RemoveItem(Guid mealItemId)
    {
        _items.RemoveAll(x => x.Id == mealItemId);
    }
}
