using Nutrition.Core.Domain.Enums;
using Nutrition.Core.Domain.Exceptions;

namespace Nutrition.Core.Domain.ValueObjects;

public readonly record struct Portion
{
    public Portion(decimal amount, PortionUnit unit)
    {
        if (amount <= 0)
        {
            throw new DomainValidationException("Portion amount must be greater than zero.");
        }

        Amount = amount;
        Unit = unit;
    }

    public decimal Amount { get; }

    public PortionUnit Unit { get; }
}