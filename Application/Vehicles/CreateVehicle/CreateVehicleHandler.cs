using GarageFlow.SharedKernel.Domain.Exceptions;
using GarageFlow.SharedKernel.Persistence;
using GarageFlow.Domain.Customers.Repositories;
using GarageFlow.Domain.Customers.ValueObjects;
using GarageFlow.Domain.Vehicles.Entities;
using GarageFlow.Domain.Vehicles.Repositories;
using GarageFlow.Domain.Vehicles.ValueObjects;
using Mediator;

namespace GarageFlow.Application.Vehicles.CreateVehicle;

public sealed class CreateVehicleHandler(
    IVehicleRepository vehicleRepository,
    ICustomerRepository customerRepository,
    IVehicleModelRepository vehicleModelRepository,
    IVehicleColorRepository vehicleColorRepository,
    IUnitOfWork unitOfWork) : IRequestHandler<CreateVehicleCommand, CreateVehicleResult>
{
    private readonly IVehicleRepository _vehicleRepository = vehicleRepository ?? throw new ArgumentNullException(nameof(vehicleRepository));
    private readonly ICustomerRepository _customerRepository = customerRepository ?? throw new ArgumentNullException(nameof(customerRepository));
    private readonly IVehicleModelRepository _vehicleModelRepository = vehicleModelRepository ?? throw new ArgumentNullException(nameof(vehicleModelRepository));
    private readonly IVehicleColorRepository _vehicleColorRepository = vehicleColorRepository ?? throw new ArgumentNullException(nameof(vehicleColorRepository));
    private readonly IUnitOfWork _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));

    public async ValueTask<CreateVehicleResult> Handle(CreateVehicleCommand request, CancellationToken cancellationToken)
    {
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

        var exists = await _vehicleRepository.ExistsByLicensePlateAsync(licensePlate, cancellationToken);
        if (exists)
        {
            throw new BusinessRuleViolationException($"A vehicle with license plate '{licensePlate.Value}' already exists.");
        }

        await _unitOfWork.BeginTransactionAsync(cancellationToken);

        try
        {
            var vehicle = Vehicle.Create(customerId, vehicleYear, vehicleModel.VehicleBrandId, vehicleModelId, vehicleColorId, licensePlate);
            await _vehicleRepository.AddAsync(vehicle, cancellationToken);
            await _unitOfWork.CommitTransactionAsync(cancellationToken);

            return new CreateVehicleResult(
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
