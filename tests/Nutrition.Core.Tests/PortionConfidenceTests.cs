using Nutrition.Core.Domain.Enums;
using Nutrition.Core.Domain.Exceptions;
using Nutrition.Core.Domain.ValueObjects;

namespace Nutrition.Core.Tests;

public sealed class PortionConfidenceTests
{
    [Fact]
    public void Portion_Throws_WhenAmountIsNotPositive()
    {
        Assert.Throws<DomainValidationException>(() => new Portion(0, PortionUnit.Gram));
        Assert.Throws<DomainValidationException>(() => new Portion(-1, PortionUnit.Gram));
    }

    [Fact]
    public void ConfidenceScore_RejectsValuesOutsideUnitInterval()
    {
        Assert.Throws<DomainValidationException>(() => new ConfidenceScore(-0.1m));
        Assert.Throws<DomainValidationException>(() => new ConfidenceScore(1.1m));
    }

    [Fact]
    public void ConfidenceScore_UsesConfirmationThreshold()
    {
        Assert.True(new ConfidenceScore(0.69m).RequiresConfirmation);
        Assert.False(new ConfidenceScore(0.70m).RequiresConfirmation);
        Assert.False(new ConfidenceScore(1m).RequiresConfirmation);
    }
}