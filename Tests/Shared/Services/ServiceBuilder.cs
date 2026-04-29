using GarageFlow.BuildingBlocks.Domain.ValueObjects;
using GarageFlow.Domain.Services.Entities;

namespace GarageFlow.Tests.Shared.Services;

public sealed class ServiceBuilder
{
    private string _description = "Oil change";
    private decimal _price = 129.90m;

    public ServiceBuilder WithDescription(string description)
    {
        _description = description;
        return this;
    }

    public ServiceBuilder WithPrice(decimal price)
    {
        _price = price;
        return this;
    }

    public Service Build()
    {
        return Service.Create(
            Description.Create(_description),
            Price.Create(_price));
    }

    public CreateServiceRequest BuildCreateRequest()
    {
        return new CreateServiceRequest(
            Description: _description,
            Price: _price);
    }

    public UpdateServiceRequest BuildUpdateRequest()
    {
        return new UpdateServiceRequest(
            Description: _description,
            Price: _price);
    }
}

public sealed record CreateServiceRequest(
    string Description,
    decimal Price);

public sealed record UpdateServiceRequest(
    string Description,
    decimal Price);
