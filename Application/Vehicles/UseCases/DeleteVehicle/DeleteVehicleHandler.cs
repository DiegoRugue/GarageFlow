using GarageFlow.SharedKernel.Domain.Exceptions;
using GarageFlow.Application.Vehicles.Ports;
using GarageFlow.Domain.Vehicles.ValueObjects;
using Mediator;

namespace GarageFlow.Application.Vehicles.UseCases.DeleteVehicle;

public sealed class DeleteVehicleHandler(
    IVehicleRepository vehicleRepository) : IRequestHandler<DeleteVehicleCommand, Unit>
{
    private readonly IVehicleRepository _vehicleRepository = vehicleRepository ?? throw new ArgumentNullException(nameof(vehicleRepository));

    public async ValueTask<Unit> Handle(DeleteVehicleCommand request, CancellationToken cancellationToken)
    {
        var vehicleId = VehicleId.From(request.Id);
        var vehicle = await _vehicleRepository.GetByIdAsync(vehicleId, cancellationToken);
        if (vehicle is null)
        {
            throw new NotFoundException($"Vehicle with ID '{request.Id}' was not found.");
        }

        vehicle.Delete();
        _vehicleRepository.Remove(vehicle);

        return Unit.Value;
    }
}
