using GarageFlow.BuildingBlocks.Domain.Exceptions;
using GarageFlow.BuildingBlocks.Persistence;
using GarageFlow.Domain.Customers.Repositories;
using GarageFlow.Domain.Customers.ValueObjects;
using GarageFlow.Domain.Vehicles.Repositories;
using GarageFlow.Domain.Vehicles.ValueObjects;
using Mediator;

namespace GarageFlow.Application.Vehicles.UpdateVehicle;

public sealed class UpdateVehicleHandler(
    IVehicleRepository vehicleRepository,
    ICustomerRepository customerRepository,
    IVehicleModelRepository vehicleModelRepository,
    IVehicleColorRepository vehicleColorRepository,
    IUnitOfWork unitOfWork) : IRequestHandler<UpdateVehicleCommand, UpdateVehicleResult>
{
    private readonly IVehicleRepository _vehicleRepository = vehicleRepository ?? throw new ArgumentNullException(nameof(vehicleRepository));
    private readonly ICustomerRepository _customerRepository = customerRepository ?? throw new ArgumentNullException(nameof(customerRepository));
    private readonly IVehicleModelRepository _vehicleModelRepository = vehicleModelRepository ?? throw new ArgumentNullException(nameof(vehicleModelRepository));
    private readonly IVehicleColorRepository _vehicleColorRepository = vehicleColorRepository ?? throw new ArgumentNullException(nameof(vehicleColorRepository));
    private readonly IUnitOfWork _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));

    public async ValueTask<UpdateVehicleResult> Handle(UpdateVehicleCommand request, CancellationToken cancellationToken)
    {
        var vehicleId = VehicleId.From(request.Id);
        var vehicle = await _vehicleRepository.GetByIdAsync(vehicleId, cancellationToken);
        if (vehicle is null)
        {
            throw new NotFoundException($"Vehicle with ID '{request.Id}' was not found.");
        }

        var customerId = CustomerId.From(request.CustomerId);
        var customer = await _customerRepository.GetByIdAsync(customerId, cancellationToken);
        if (customer is null)
        {
            throw new NotFoundException($"Customer with ID '{request.CustomerId}' was not found.");
        }

        var vehicleModelId = VehicleModelId.From(request.VehicleModelId);
        var vehicleModel = await _vehicleModelRepository.GetByIdAsync(vehicleModelId, cancellationToken);
        if (vehicleModel is null)
        {
            throw new NotFoundException($"Vehicle model with ID '{request.VehicleModelId}' was not found.");
        }

        var vehicleColorId = VehicleColorId.From(request.VehicleColorId);
        var vehicleColor = await _vehicleColorRepository.GetByIdAsync(vehicleColorId, cancellationToken);
        if (vehicleColor is null)
        {
            throw new NotFoundException($"Vehicle color with ID '{request.VehicleColorId}' was not found.");
        }

        var licensePlate = LicensePlate.Create(request.Plate);
        var vehicleYear = VehicleYear.Create(request.Year);

        var exists = await _vehicleRepository.ExistsByLicensePlateAsync(licensePlate, vehicleId, cancellationToken);
        if (exists)
        {
            throw new BusinessRuleViolationException($"A vehicle with license plate '{licensePlate.Value}' already exists.");
        }

        await _unitOfWork.BeginTransactionAsync(cancellationToken);

        try
        {
            vehicle.Update(customerId, vehicleYear, vehicleModel.VehicleBrandId, vehicleModelId, vehicleColorId, licensePlate);
            await _unitOfWork.CommitTransactionAsync(cancellationToken);

            return new UpdateVehicleResult(
                Id: vehicle.Id.Value,
                CustomerId: vehicle.CustomerId.Value,
                Year: vehicle.Year.Value,
                VehicleBrandId: vehicle.VehicleBrandId.Value,
                VehicleModelId: vehicle.VehicleModelId.Value,
                VehicleColorId: vehicle.VehicleColorId.Value,
                Plate: vehicle.LicensePlate.Value,
                CreatedAt: vehicle.CreatedAt);
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            throw;
        }
    }
}
