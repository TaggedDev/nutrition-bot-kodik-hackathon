using Nutrition.Core.Domain.Exceptions;

namespace Nutrition.Core.Domain.ValueObjects;

public readonly record struct ConfidenceScore
{
    public const decimal ConfirmationThreshold = 0.70m;

    public ConfidenceScore(decimal value)
    {
        if (value < 0m || value > 1m)
        {
            throw new DomainValidationException("Confidence score must be between 0 and 1.");
        }

        Value = value;
    }

    public decimal Value { get; }

    public bool RequiresConfirmation => Value < ConfirmationThreshold;
}