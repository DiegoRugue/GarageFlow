using GarageFlow.Domain.Vehicles.Entities;
using GarageFlow.Domain.Vehicles.ValueObjects;

namespace GarageFlow.Tests.Shared.Vehicles;

public sealed class VehicleModelBuilder
{
    private Guid _vehicleBrandId = Guid.NewGuid();
    private string _name = "Uno";

    public VehicleModelBuilder WithVehicleBrandId(Guid vehicleBrandId)
    {
        _vehicleBrandId = vehicleBrandId;
        return this;
    }

    public VehicleModelBuilder WithName(string name)
    {
        _name = name;
        return this;
    }

    public VehicleModel Build()
    {
        return VehicleModel.Create(
            VehicleBrandId.From(_vehicleBrandId),
            _name);
    }

    public CreateVehicleModelRequest BuildCreateRequest()
    {
        return new CreateVehicleModelRequest(
            VehicleBrandId: _vehicleBrandId,
            Name: _name);
    }

    public UpdateVehicleModelRequest BuildUpdateRequest()
    {
        return new UpdateVehicleModelRequest(
            VehicleBrandId: _vehicleBrandId,
            Name: _name);
    }
}

public sealed record CreateVehicleModelRequest(
    Guid VehicleBrandId,
    string Name);

public sealed record UpdateVehicleModelRequest(
    Guid VehicleBrandId,
    string Name);
