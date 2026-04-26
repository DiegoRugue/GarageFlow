using GarageFlow.Domain.Vehicles.Entities;

namespace GarageFlow.Tests.Shared.Vehicles;

public sealed class VehicleBrandBuilder
{
    private string _name = "Fiat";

    public VehicleBrandBuilder WithName(string name)
    {
        _name = name;
        return this;
    }

    public VehicleBrand Build()
    {
        return VehicleBrand.Create(_name);
    }

    public CreateVehicleBrandRequest BuildCreateRequest()
    {
        return new CreateVehicleBrandRequest(Name: _name);
    }

    public UpdateVehicleBrandRequest BuildUpdateRequest()
    {
        return new UpdateVehicleBrandRequest(Name: _name);
    }
}

public sealed record CreateVehicleBrandRequest(string Name);

public sealed record UpdateVehicleBrandRequest(string Name);
