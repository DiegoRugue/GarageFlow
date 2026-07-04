using GarageFlow.SharedKernel.Domain.Exceptions;
using GarageFlow.Domain.Services.ValueObjects;

namespace GarageFlow.Tests.Unit.Services;

public class ServiceValueObjectsTests
{
    [Fact]
    public void ServiceDescription_Create_ShouldTrimAndReturnValue()
    {
        var description = ServiceDescription.Create("  Synthetic oil change  ");

        Assert.Equal("Synthetic oil change", description.Value);
        Assert.Equal("Synthetic oil change", description.ToString());
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void ServiceDescription_Create_ShouldThrowValidationException_WhenValueIsEmpty(string value)
    {
        Assert.Throws<ValidationException>(() => ServiceDescription.Create(value));
    }

    [Fact]
    public void ServiceDescription_Create_ShouldThrowValidationException_WhenValueExceedsMaxLength()
    {
        var value = new string('x', ServiceDescription.MaxLength + 1);

        Assert.Throws<ValidationException>(() => ServiceDescription.Create(value));
    }

    [Fact]
    public void ServicePrice_Create_ShouldReturnValueAndFormat_WhenValueIsValid()
    {
        var price = ServicePrice.Create(250.5m);

        decimal converted = price;

        Assert.Equal(250.5m, price.Value);
        Assert.Equal(250.5m, converted);
        Assert.Equal("250.50", price.ToString());
    }

    [Fact]
    public void ServicePrice_Create_ShouldThrowValidationException_WhenValueIsNegative()
    {
        Assert.Throws<ValidationException>(() => ServicePrice.Create(-1m));
    }

    [Fact]
    public void ServicePrice_Create_ShouldThrowValidationException_WhenValueHasMoreThanTwoDecimalPlaces()
    {
        Assert.Throws<ValidationException>(() => ServicePrice.Create(10.999m));
    }
}
