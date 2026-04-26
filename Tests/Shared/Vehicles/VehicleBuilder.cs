using GarageFlow.Domain.Customers.ValueObjects;
using GarageFlow.Domain.Vehicles.Entities;
using GarageFlow.Domain.Vehicles.ValueObjects;

namespace GarageFlow.Tests.Shared.Vehicles;

public sealed class VehicleBuilder
{
    private string _plate = "ABC1234";
    private int _year = 2024;
    private Guid _customerId = Guid.NewGuid();
    private Guid _vehicleBrandId = Guid.NewGuid();
    private Guid _vehicleModelId = Guid.NewGuid();
    private Guid _vehicleColorId = Guid.NewGuid();

    public VehicleBuilder WithPlate(string plate)
    {
        _plate = plate;
        return this;
    }

    public VehicleBuilder WithYear(int year)
    {
        _year = year;
        return this;
    }

    public VehicleBuilder WithCustomerId(Guid customerId)
    {
        _customerId = customerId;
        return this;
    }

    public VehicleBuilder WithVehicleBrandId(Guid vehicleBrandId)
    {
        _vehicleBrandId = vehicleBrandId;
        return this;
    }

    public VehicleBuilder WithVehicleModelId(Guid vehicleModelId)
    {
        _vehicleModelId = vehicleModelId;
        return this;
    }

    public VehicleBuilder WithVehicleColorId(Guid vehicleColorId)
    {
        _vehicleColorId = vehicleColorId;
        return this;
    }

    public VehicleBuilder WithDependencies(
        Guid customerId,
        Guid vehicleBrandId,
        Guid vehicleModelId,
        Guid vehicleColorId)
    {
        _customerId = customerId;
        _vehicleBrandId = vehicleBrandId;
        _vehicleModelId = vehicleModelId;
        _vehicleColorId = vehicleColorId;
        return this;
    }

    public Vehicle Build()
    {
        return Vehicle.Create(
            CustomerId.From(_customerId),
            _year,
            VehicleBrandId.From(_vehicleBrandId),
            VehicleModelId.From(_vehicleModelId),
            VehicleColorId.From(_vehicleColorId),
            LicensePlate.Create(_plate));
    }

    public CreateVehicleRequest BuildCreateRequest()
    {
        return new CreateVehicleRequest(
            Plate: _plate,
            Year: _year,
            CustomerId: _customerId,
            VehicleModelId: _vehicleModelId,
            VehicleColorId: _vehicleColorId);
    }

    public UpdateVehicleRequest BuildUpdateRequest()
    {
        return new UpdateVehicleRequest(
            Plate: _plate,
            Year: _year,
            CustomerId: _customerId,
            VehicleModelId: _vehicleModelId,
            VehicleColorId: _vehicleColorId);
    }
}

public sealed record CreateVehicleRequest(
    string Plate,
    int Year,
    Guid CustomerId,
    Guid VehicleModelId,
    Guid VehicleColorId);

public sealed record UpdateVehicleRequest(
    string Plate,
    int Year,
    Guid CustomerId,
    Guid VehicleModelId,
    Guid VehicleColorId);
