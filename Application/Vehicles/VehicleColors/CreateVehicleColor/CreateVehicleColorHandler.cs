using GarageFlow.BuildingBlocks.Domain.Exceptions;
using GarageFlow.BuildingBlocks.Persistence;
using GarageFlow.Domain.Vehicles.Entities;
using GarageFlow.Domain.Vehicles.Repositories;
using Mediator;

namespace GarageFlow.Application.Vehicles.VehicleColors.CreateVehicleColor;

public sealed class CreateVehicleColorHandler(
    IVehicleColorRepository vehicleColorRepository,
    IUnitOfWork unitOfWork) : IRequestHandler<CreateVehicleColorCommand, CreateVehicleColorResult>
{
    private readonly IVehicleColorRepository _vehicleColorRepository = vehicleColorRepository ?? throw new ArgumentNullException(nameof(vehicleColorRepository));
    private readonly IUnitOfWork _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));

    public async ValueTask<CreateVehicleColorResult> Handle(CreateVehicleColorCommand request, CancellationToken cancellationToken)
    {
        var vehicleColor = VehicleColor.Create(request.Name);

        var exists = await _vehicleColorRepository.ExistsByNameAsync(vehicleColor.Name, cancellationToken);
        if (exists)
        {
            throw new BusinessRuleViolationException($"A vehicle color with name '{vehicleColor.Name}' already exists.");
        }

        await _unitOfWork.BeginTransactionAsync(cancellationToken);

        try
        {
            await _vehicleColorRepository.AddAsync(vehicleColor, cancellationToken);
            await _unitOfWork.CommitTransactionAsync(cancellationToken);

            return new CreateVehicleColorResult(
                Id: vehicleColor.Id.Value,
                Name: vehicleColor.Name,
                CreatedAt: vehicleColor.CreatedAt);
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            throw;
        }
    }
}
