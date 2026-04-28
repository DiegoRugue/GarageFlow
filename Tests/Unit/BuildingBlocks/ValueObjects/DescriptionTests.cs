using GarageFlow.BuildingBlocks.Domain.Exceptions;
using GarageFlow.BuildingBlocks.Domain.ValueObjects;

namespace GarageFlow.Tests.Unit.BuildingBlocks.ValueObjects;

public class DescriptionTests
{
    [Fact]
    public void Create_ShouldAcceptDescriptionAtMaxLength()
    {
        var description = Description.Create(new string('x', Description.MaxLength));

        Assert.Equal(Description.MaxLength, description.Value.Length);
    }

    [Fact]
    public void Create_ShouldNormalizeAndReturnValue()
    {
        var description = Description.Create("  Synthetic engine oil  ");

        Assert.Equal("Synthetic engine oil", description.Value);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_ShouldThrowValidationException_WhenEmptyOrWhitespace(string value)
    {
        Assert.Throws<ValidationException>(() => Description.Create(value));
    }

    [Fact]
    public void Create_ShouldThrowValidationException_WhenLongerThanMaxLength()
    {
        var value = new string('x', Description.MaxLength + 1);

        Assert.Throws<ValidationException>(() => Description.Create(value));
    }
}
