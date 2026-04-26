using GarageFlow.Domain.Vehicles.Repositories;
using GarageFlow.Domain.Vehicles.ValueObjects;
using Mediator;

namespace GarageFlow.Application.Vehicles.VehicleColors.GetVehicleColorById;

public sealed class GetVehicleColorByIdHandler(
    IVehicleColorRepository vehicleColorRepository) : IRequestHandler<GetVehicleColorByIdQuery, VehicleColorDto?>
{
    private readonly IVehicleColorRepository _vehicleColorRepository = vehicleColorRepository ?? throw new ArgumentNullException(nameof(vehicleColorRepository));

    public async ValueTask<VehicleColorDto?> Handle(GetVehicleColorByIdQuery request, CancellationToken cancellationToken)
    {
        var vehicleColorId = VehicleColorId.From(request.Id);
        var vehicleColor = await _vehicleColorRepository.GetByIdAsync(vehicleColorId, cancellationToken);
        if (vehicleColor is null)
        {
            return null;
        }

        return new VehicleColorDto(
            Id: vehicleColor.Id.Value,
            Name: vehicleColor.Name,
            CreatedAt: vehicleColor.CreatedAt);
    }
}
