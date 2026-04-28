using GarageFlow.BuildingBlocks.Domain.Entities;
using GarageFlow.BuildingBlocks.Domain.Exceptions;
using GarageFlow.BuildingBlocks.Domain.Interfaces;
using GarageFlow.Domain.Customers.ValueObjects;
using GarageFlow.Domain.Vehicles.Events;
using GarageFlow.Domain.Vehicles.ValueObjects;

namespace GarageFlow.Domain.Vehicles.Entities;

public sealed class Vehicle : Entity<VehicleId>, IAggregateRoot
{
    public CustomerId CustomerId { get; private set; }
    public VehicleYear Year { get; private set; }
    public VehicleBrandId VehicleBrandId { get; private set; }
    public VehicleModelId VehicleModelId { get; private set; }
    public VehicleColorId VehicleColorId { get; private set; }
    public LicensePlate LicensePlate { get; private set; }

    private Vehicle(VehicleId id) : base(id)
    {
        LicensePlate = null!;
    }

    private Vehicle(
        VehicleId id,
        CustomerId customerId,
        VehicleYear year,
        VehicleBrandId vehicleBrandId,
        VehicleModelId vehicleModelId,
        VehicleColorId vehicleColorId,
        LicensePlate licensePlate) : base(id)
    {
        CustomerId = EnsureValidCustomerId(customerId);
        Year = EnsureValidYear(year);
        VehicleBrandId = EnsureValidBrandId(vehicleBrandId);
        VehicleModelId = EnsureValidModelId(vehicleModelId);
        VehicleColorId = EnsureValidColorId(vehicleColorId);
        LicensePlate = EnsureLicensePlate(licensePlate);
    }

    public static Vehicle Create(
        CustomerId customerId,
        int year,
        VehicleBrandId vehicleBrandId,
        VehicleModelId vehicleModelId,
        VehicleColorId vehicleColorId,
        LicensePlate licensePlate)
    {
        return Create(
            customerId,
            VehicleYear.Create(year),
            vehicleBrandId,
            vehicleModelId,
            vehicleColorId,
            licensePlate);
    }

    public static Vehicle Create(
        CustomerId customerId,
        VehicleYear year,
        VehicleBrandId vehicleBrandId,
        VehicleModelId vehicleModelId,
        VehicleColorId vehicleColorId,
        LicensePlate licensePlate)
    {
        var id = VehicleId.New();
        var validatedCustomerId = EnsureValidCustomerId(customerId);
        var validatedYear = EnsureValidYear(year);
        var validatedBrandId = EnsureValidBrandId(vehicleBrandId);
        var validatedModelId = EnsureValidModelId(vehicleModelId);
        var validatedColorId = EnsureValidColorId(vehicleColorId);
        var validatedLicensePlate = EnsureLicensePlate(licensePlate);
        var vehicle = new Vehicle(
            id,
            validatedCustomerId,
            validatedYear,
            validatedBrandId,
            validatedModelId,
            validatedColorId,
            validatedLicensePlate);

        vehicle.RaiseDomainEvent(new VehicleCreated(
            VehicleId: id,
            CustomerId: validatedCustomerId,
            Year: validatedYear.Value,
            VehicleBrandId: validatedBrandId,
            VehicleModelId: validatedModelId,
            VehicleColorId: validatedColorId,
            LicensePlate: validatedLicensePlate.Value,
            CreatedAt: vehicle.CreatedAt));

        return vehicle;
    }

    public void Update(
        CustomerId customerId,
        int year,
        VehicleBrandId vehicleBrandId,
        VehicleModelId vehicleModelId,
        VehicleColorId vehicleColorId,
        LicensePlate licensePlate)
    {
        Update(
            customerId,
            VehicleYear.Create(year),
            vehicleBrandId,
            vehicleModelId,
            vehicleColorId,
            licensePlate);
    }

    public void Update(
        CustomerId customerId,
        VehicleYear year,
        VehicleBrandId vehicleBrandId,
        VehicleModelId vehicleModelId,
        VehicleColorId vehicleColorId,
        LicensePlate licensePlate)
    {
        CustomerId = EnsureValidCustomerId(customerId);
        Year = EnsureValidYear(year);
        VehicleBrandId = EnsureValidBrandId(vehicleBrandId);
        VehicleModelId = EnsureValidModelId(vehicleModelId);
        VehicleColorId = EnsureValidColorId(vehicleColorId);
        LicensePlate = EnsureLicensePlate(licensePlate);
        UpdatedAt = DateTime.UtcNow;

        RaiseDomainEvent(new VehicleUpdated(
            VehicleId: Id,
            CustomerId: CustomerId,
            Year: Year.Value,
            VehicleBrandId: VehicleBrandId,
            VehicleModelId: VehicleModelId,
            VehicleColorId: VehicleColorId,
            LicensePlate: LicensePlate.Value));
    }

    public void Delete()
    {
        UpdatedAt = DateTime.UtcNow;

        RaiseDomainEvent(new VehicleDeleted(
            VehicleId: Id,
            LicensePlate: LicensePlate.Value));
    }

    private static CustomerId EnsureValidCustomerId(CustomerId customerId)
    {
        if (customerId.Value == Guid.Empty)
        {
            throw new ValidationException("Customer identifier cannot be empty.");
        }

        return customerId;
    }

    private static VehicleYear EnsureValidYear(VehicleYear year) => VehicleYear.Create(year.Value);

    private static VehicleBrandId EnsureValidBrandId(VehicleBrandId vehicleBrandId)
    {
        if (vehicleBrandId.Value == Guid.Empty)
        {
            throw new ValidationException("Vehicle brand identifier cannot be empty.");
        }

        return vehicleBrandId;
    }

    private static VehicleModelId EnsureValidModelId(VehicleModelId vehicleModelId)
    {
        if (vehicleModelId.Value == Guid.Empty)
        {
            throw new ValidationException("Vehicle model identifier cannot be empty.");
        }

        return vehicleModelId;
    }

    private static VehicleColorId EnsureValidColorId(VehicleColorId vehicleColorId)
    {
        if (vehicleColorId.Value == Guid.Empty)
        {
            throw new ValidationException("Vehicle color identifier cannot be empty.");
        }

        return vehicleColorId;
    }

    private static LicensePlate EnsureLicensePlate(LicensePlate licensePlate)
    {
        if (licensePlate is null)
        {
            throw new ValidationException("License plate cannot be null.");
        }

        return licensePlate;
    }
}
