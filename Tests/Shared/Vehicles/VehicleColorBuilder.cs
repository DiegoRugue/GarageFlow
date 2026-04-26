using GarageFlow.Domain.Vehicles.Entities;

namespace GarageFlow.Tests.Shared.Vehicles;

public sealed class VehicleColorBuilder
{
    private string _name = "Black";

    public VehicleColorBuilder WithName(string name)
    {
        _name = name;
        return this;
    }

    public VehicleColor Build()
    {
        return VehicleColor.Create(_name);
    }

    public CreateVehicleColorRequest BuildCreateRequest()
    {
        return new CreateVehicleColorRequest(Name: _name);
    }

    public UpdateVehicleColorRequest BuildUpdateRequest()
    {
        return new UpdateVehicleColorRequest(Name: _name);
    }
}

public sealed record CreateVehicleColorRequest(string Name);

public sealed record UpdateVehicleColorRequest(string Name);
