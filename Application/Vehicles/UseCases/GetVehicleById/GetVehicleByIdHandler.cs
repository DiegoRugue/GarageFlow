using GarageFlow.SharedKernel.Domain.Exceptions;
using GarageFlow.Application.Vehicles.Ports;
using GarageFlow.Domain.Vehicles.ValueObjects;
using Mediator;

namespace GarageFlow.Application.Vehicles.UseCases.GetVehicleById;

public sealed class GetVehicleByIdHandler(
    IVehicleQueries vehicleQueries) : IRequestHandler<GetVehicleByIdQuery, VehicleDto>
{
    private readonly IVehicleQueries _vehicleQueries = vehicleQueries ?? throw new ArgumentNullException(nameof(vehicleQueries));

    public async ValueTask<VehicleDto> Handle(GetVehicleByIdQuery request, CancellationToken cancellationToken)
    {
        var vehicleId = VehicleId.From(request.Id);
        var vehicle = await _vehicleQueries.GetDetailsByIdAsync(vehicleId, cancellationToken);

        if (vehicle is null)
        {
            throw new NotFoundException($"Vehicle with ID '{request.Id}' was not found.");
        }

        return new VehicleDto(
            Id: vehicle.Id,
            CustomerId: vehicle.CustomerId,
            Year: vehicle.Year,
            VehicleBrandId: vehicle.VehicleBrandId,
            VehicleBrandName: vehicle.VehicleBrandName,
            VehicleModelId: vehicle.VehicleModelId,
            VehicleModelName: vehicle.VehicleModelName,
            VehicleColorId: vehicle.VehicleColorId,
            VehicleColorName: vehicle.VehicleColorName,
            Plate: vehicle.Plate,
            CreatedAt: vehicle.CreatedAt);
    }
}
