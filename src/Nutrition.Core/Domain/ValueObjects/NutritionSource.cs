using Nutrition.Core.Domain.Enums;
using Nutrition.Core.Domain.Exceptions;

namespace Nutrition.Core.Domain.ValueObjects;

public sealed record NutritionSource
{
    public NutritionSource(NutritionSourceType type, string reference)
    {
        if (string.IsNullOrWhiteSpace(reference))
        {
            throw new DomainValidationException("Nutrition source reference is required.");
        }

        Type = type;
        Reference = reference.Trim();
    }

    public NutritionSourceType Type { get; }

    public string Reference { get; }
}