using GarageFlow.SharedKernel.Domain.Exceptions;
using GarageFlow.SharedKernel.Domain.ValueObjects;

namespace GarageFlow.Tests.Unit.SharedKernel.ValueObjects;

public class PriceTests
{
    [Fact]
    public void Create_ShouldReturnCorrectValue()
    {
        var price = Price.Create(129.90m);

        Assert.Equal(129.90m, price.Value);
    }

    [Fact]
    public void Create_ShouldAcceptZeroValue()
    {
        var price = Price.Create(0m);

        Assert.Equal(0m, price.Value);
    }

    [Fact]
    public void Create_ShouldAcceptExactlyTwoDecimalPlaces()
    {
        var price = Price.Create(12.34m);

        Assert.Equal(12.34m, price.Value);
    }

    [Fact]
    public void Create_ShouldThrowValidationException_WhenNegative()
    {
        Assert.Throws<ValidationException>(() => Price.Create(-1m));
    }

    [Fact]
    public void Create_ShouldThrowValidationException_WhenMoreThanTwoDecimalPlaces()
    {
        Assert.Throws<ValidationException>(() => Price.Create(120.999m));
    }
}
