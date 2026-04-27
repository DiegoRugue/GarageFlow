using GarageFlow.BuildingBlocks.Domain.Entities;
using GarageFlow.BuildingBlocks.Domain.Interfaces;
using GarageFlow.Domain.Services.Events;
using GarageFlow.Domain.Services.ValueObjects;

namespace GarageFlow.Domain.Services.Entities;

public sealed class Service : Entity<ServiceId>, IAggregateRoot
{
    public ServiceDescription Description { get; private set; }
    public ServicePrice Price { get; private set; }

    private Service(ServiceId id) : base(id)
    {
        Description = null!;
        Price = null!;
    }

    private Service(
        ServiceId id,
        ServiceDescription description,
        ServicePrice price) : base(id)
    {
        Description = description;
        Price = price;
    }

    public static Service Create(ServiceDescription description, ServicePrice price)
    {
        var id = ServiceId.New();
        var service = new Service(id, description, price);

        service.RaiseDomainEvent(new ServiceCreated(
            ServiceId: id,
            Description: description.Value,
            Price: price.Value,
            CreatedAt: service.CreatedAt));

        return service;
    }

    public void Update(ServiceDescription description, ServicePrice price)
    {
        Description = description;
        Price = price;
        UpdatedAt = DateTime.UtcNow;

        RaiseDomainEvent(new ServiceUpdated(
            ServiceId: Id,
            Description: description.Value,
            Price: price.Value));
    }

    public void Delete()
    {
        UpdatedAt = DateTime.UtcNow;

        RaiseDomainEvent(new ServiceDeleted(
            ServiceId: Id,
            Description: Description.Value));
    }
}
