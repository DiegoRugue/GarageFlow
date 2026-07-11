using GarageFlow.SharedKernel.Domain.Exceptions;
using GarageFlow.Domain.InventoryItems.ValueObjects;

namespace GarageFlow.Tests.Unit.InventoryItems;

public class InventoryItemValueObjectsTests
{
    [Fact]
    public void InventoryItemName_Create_ShouldNormalizeAndReturnValue()
    {
        var name = InventoryItemName.Create("  Brake Pad  ");

        Assert.Equal("Brake Pad", name.Value);
    }

    [Fact]
    public void InventoryItemName_Create_ShouldAcceptNameAtMaxLength()
    {
        var name = InventoryItemName.Create(new string('x', InventoryItemName.MaxLength));

        Assert.Equal(InventoryItemName.MaxLength, name.Value.Length);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void InventoryItemName_Create_ShouldThrowValidationException_WhenEmptyOrWhitespace(string value)
    {
        Assert.Throws<ValidationException>(() => InventoryItemName.Create(value));
    }

    [Fact]
    public void InventoryItemName_Create_ShouldThrowValidationException_WhenLongerThanMaxLength()
    {
        var value = new string('x', InventoryItemName.MaxLength + 1);

        Assert.Throws<ValidationException>(() => InventoryItemName.Create(value));
    }

    [Fact]
    public void InventoryItemStockQuantity_Create_ShouldReturnValue_WhenValueIsZero()
    {
        var stockQuantity = InventoryItemStockQuantity.Create(0);

        Assert.Equal(0, stockQuantity.Value);
    }

    [Fact]
    public void InventoryItemStockQuantity_Create_ShouldReturnValue_WhenValueIsPositive()
    {
        var stockQuantity = InventoryItemStockQuantity.Create(12);

        Assert.Equal(12, stockQuantity.Value);
    }

    [Fact]
    public void InventoryItemStockQuantity_Create_ShouldThrowValidationException_WhenValueIsNegative()
    {
        Assert.Throws<ValidationException>(() => InventoryItemStockQuantity.Create(-1));
    }
}
