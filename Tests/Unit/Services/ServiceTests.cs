using GarageFlow.BuildingBlocks.Domain.Exceptions;
using GarageFlow.BuildingBlocks.Domain.ValueObjects;
using GarageFlow.Domain.Services.Entities;
using GarageFlow.Domain.Services.Events;

namespace GarageFlow.Tests.Unit.Services;

public class ServiceTests
{
    [Fact]
    public void Create_ShouldRaiseServiceCreatedEvent()
    {
        var description = Description.Create("Oil change");
        var price = Price.Create(129.90m);

        var service = Service.Create(description, price);

        var createdEvent = Assert.Single(service.DomainEvents.OfType<ServiceCreated>());
        Assert.Equal(service.Id, createdEvent.ServiceId);
        Assert.Equal(description.Value, createdEvent.Description);
        Assert.Equal(price.Value, createdEvent.Price);
    }

    [Fact]
    public void Update_ShouldRaiseServiceUpdatedEvent()
    {
        var service = Service.Create(
            Description.Create("Oil change"),
            Price.Create(129.90m));
        var originalUpdatedAt = service.UpdatedAt;

        var updatedDescription = Description.Create("Premium oil change");
        var updatedPrice = Price.Create(199.90m);

        WaitUntilTimeAdvances(originalUpdatedAt);
        service.Update(updatedDescription, updatedPrice);

        var updatedEvent = Assert.Single(service.DomainEvents.OfType<ServiceUpdated>());
        Assert.Equal(service.Id, updatedEvent.ServiceId);
        Assert.Equal(updatedDescription.Value, updatedEvent.Description);
        Assert.Equal(updatedPrice.Value, updatedEvent.Price);
        Assert.Equal(updatedDescription, service.Description);
        Assert.Equal(updatedPrice, service.Price);
        Assert.True(service.UpdatedAt > originalUpdatedAt);
    }

    [Fact]
    public void Delete_ShouldRaiseServiceDeletedEvent()
    {
        var service = Service.Create(
            Description.Create("Oil change"),
            Price.Create(129.90m));
        var originalUpdatedAt = service.UpdatedAt;

        WaitUntilTimeAdvances(originalUpdatedAt);
        service.Delete();

        var deletedEvent = Assert.Single(service.DomainEvents.OfType<ServiceDeleted>());
        Assert.Equal(service.Id, deletedEvent.ServiceId);
        Assert.Equal(service.Description.Value, deletedEvent.Description);
        Assert.True(service.UpdatedAt > originalUpdatedAt);
    }

    [Fact]
    public void Description_Create_ShouldThrowValidationException_WhenEmpty()
    {
        var exception = Assert.Throws<ValidationException>(() => Description.Create(" "));

        Assert.Equal("Service description cannot be empty or whitespace.", exception.Message);
    }

    [Fact]
    public void Description_Create_ShouldThrowValidationException_WhenTooLong()
    {
        var description = new string('x', Description.MaxLength + 1);

        var exception = Assert.Throws<ValidationException>(() => Description.Create(description));

        Assert.Equal($"Service description cannot exceed {Description.MaxLength} characters.", exception.Message);
    }

    [Fact]
    public void Price_Create_ShouldThrowValidationException_WhenNegative()
    {
        var exception = Assert.Throws<ValidationException>(() => Price.Create(-0.01m));

        Assert.Equal("Service price cannot be negative.", exception.Message);
    }

    [Fact]
    public void Price_Create_ShouldThrowValidationException_WhenMoreThanTwoDecimalPlaces()
    {
        var exception = Assert.Throws<ValidationException>(() => Price.Create(120.999m));

        Assert.Equal("Service price cannot have more than 2 decimal places.", exception.Message);
    }

    private static void WaitUntilTimeAdvances(DateTime referenceUtc)
    {
        while (DateTime.UtcNow <= referenceUtc)
        {
            Thread.SpinWait(50);
        }
    }
}
