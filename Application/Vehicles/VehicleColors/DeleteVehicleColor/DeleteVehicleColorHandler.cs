using GarageFlow.BuildingBlocks.Domain.Exceptions;
using GarageFlow.BuildingBlocks.Persistence;
using GarageFlow.Domain.Vehicles.Repositories;
using GarageFlow.Domain.Vehicles.ValueObjects;
using Mediator;

namespace GarageFlow.Application.Vehicles.VehicleColors.DeleteVehicleColor;

public sealed class DeleteVehicleColorHandler(
    IVehicleColorRepository vehicleColorRepository,
    IVehicleRepository vehicleRepository,
    IUnitOfWork unitOfWork) : IRequestHandler<DeleteVehicleColorCommand, Unit>
{
    private readonly IVehicleColorRepository _vehicleColorRepository = vehicleColorRepository ?? throw new ArgumentNullException(nameof(vehicleColorRepository));
    private readonly IVehicleRepository _vehicleRepository = vehicleRepository ?? throw new ArgumentNullException(nameof(vehicleRepository));
    private readonly IUnitOfWork _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));

    public async ValueTask<Unit> Handle(DeleteVehicleColorCommand request, CancellationToken cancellationToken)
    {
        var vehicleColorId = VehicleColorId.From(request.Id);
        var vehicleColor = await _vehicleColorRepository.GetByIdAsync(vehicleColorId, cancellationToken);
        if (vehicleColor is null)
        {
            throw new NotFoundException($"Vehicle color with ID '{request.Id}' was not found.");
        }

        var hasVehicles = await _vehicleRepository.ExistsByVehicleColorIdAsync(vehicleColorId, cancellationToken);
        if (hasVehicles)
        {
            throw new BusinessRuleViolationException($"Vehicle color with ID '{request.Id}' cannot be deleted because it has related vehicles.");
        }

        await _unitOfWork.BeginTransactionAsync(cancellationToken);

        try
        {
            vehicleColor.Delete();
            _vehicleColorRepository.Remove(vehicleColor);
            await _unitOfWork.CommitTransactionAsync(cancellationToken);

            return Unit.Value;
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            throw;
        }
    }
}
