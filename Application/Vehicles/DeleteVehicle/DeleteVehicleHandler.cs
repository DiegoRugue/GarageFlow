using GarageFlow.BuildingBlocks.Domain.Exceptions;
using GarageFlow.BuildingBlocks.Persistence;
using GarageFlow.Domain.Vehicles.Repositories;
using GarageFlow.Domain.Vehicles.ValueObjects;
using Mediator;

namespace GarageFlow.Application.Vehicles.DeleteVehicle;

public sealed class DeleteVehicleHandler(
    IVehicleRepository vehicleRepository,
    IUnitOfWork unitOfWork) : IRequestHandler<DeleteVehicleCommand, Unit>
{
    private readonly IVehicleRepository _vehicleRepository = vehicleRepository ?? throw new ArgumentNullException(nameof(vehicleRepository));
    private readonly IUnitOfWork _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));

    public async ValueTask<Unit> Handle(DeleteVehicleCommand request, CancellationToken cancellationToken)
    {
        var vehicleId = VehicleId.From(request.Id);
        var vehicle = await _vehicleRepository.GetByIdAsync(vehicleId, cancellationToken);
        if (vehicle is null)
        {
            throw new NotFoundException($"Vehicle with ID '{request.Id}' was not found.");
        }

        await _unitOfWork.BeginTransactionAsync(cancellationToken);

        try
        {
            vehicle.Delete();
            _vehicleRepository.Remove(vehicle);
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
