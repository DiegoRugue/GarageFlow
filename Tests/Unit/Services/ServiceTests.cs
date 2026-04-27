using GarageFlow.BuildingBlocks.Domain.Exceptions;
using GarageFlow.Domain.Services.Entities;
using GarageFlow.Domain.Services.Events;
using GarageFlow.Domain.Services.ValueObjects;

namespace GarageFlow.Tests.Unit.Services;

public class ServiceTests
{
    [Fact]
    public void Create_ShouldRaiseServiceCreatedEvent()
    {
        var description = ServiceDescription.Create("Oil change");
        var price = ServicePrice.Create(129.90m);

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
            ServiceDescription.Create("Oil change"),
            ServicePrice.Create(129.90m));
        var originalUpdatedAt = service.UpdatedAt;

        var updatedDescription = ServiceDescription.Create("Premium oil change");
        var updatedPrice = ServicePrice.Create(199.90m);

        Thread.Sleep(10);
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
            ServiceDescription.Create("Oil change"),
            ServicePrice.Create(129.90m));
        var originalUpdatedAt = service.UpdatedAt;

        Thread.Sleep(10);
        service.Delete();

        var deletedEvent = Assert.Single(service.DomainEvents.OfType<ServiceDeleted>());
        Assert.Equal(service.Id, deletedEvent.ServiceId);
        Assert.Equal(service.Description.Value, deletedEvent.Description);
        Assert.True(service.UpdatedAt > originalUpdatedAt);
    }

    [Fact]
    public void ServiceDescription_Create_ShouldThrowValidationException_WhenEmpty()
    {
        var exception = Assert.Throws<ValidationException>(() => ServiceDescription.Create(" "));

        Assert.Equal("Service description cannot be empty or whitespace.", exception.Message);
    }

    [Fact]
    public void ServiceDescription_Create_ShouldThrowValidationException_WhenTooLong()
    {
        var description = new string('x', ServiceDescription.MaxLength + 1);

        var exception = Assert.Throws<ValidationException>(() => ServiceDescription.Create(description));

        Assert.Equal($"Service description cannot exceed {ServiceDescription.MaxLength} characters.", exception.Message);
    }

    [Fact]
    public void ServicePrice_Create_ShouldThrowValidationException_WhenNegative()
    {
        var exception = Assert.Throws<ValidationException>(() => ServicePrice.Create(-0.01m));

        Assert.Equal("Service price cannot be negative.", exception.Message);
    }

    [Fact]
    public void ServicePrice_Create_ShouldThrowValidationException_WhenMoreThanTwoDecimalPlaces()
    {
        var exception = Assert.Throws<ValidationException>(() => ServicePrice.Create(120.999m));

        Assert.Equal("Service price cannot have more than 2 decimal places.", exception.Message);
    }
}

