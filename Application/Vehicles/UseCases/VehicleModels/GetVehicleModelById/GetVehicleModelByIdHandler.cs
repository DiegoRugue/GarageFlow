using GarageFlow.SharedKernel.Domain.Exceptions;
using GarageFlow.Application.Vehicles.Ports;
using GarageFlow.Domain.Vehicles.ValueObjects;
using Mediator;

namespace GarageFlow.Application.Vehicles.UseCases.VehicleModels.GetVehicleModelById;

public sealed class GetVehicleModelByIdHandler(
    IVehicleModelRepository vehicleModelRepository) : IRequestHandler<GetVehicleModelByIdQuery, VehicleModelDto>
{
    private readonly IVehicleModelRepository _vehicleModelRepository = vehicleModelRepository ?? throw new ArgumentNullException(nameof(vehicleModelRepository));

    public async ValueTask<VehicleModelDto> Handle(GetVehicleModelByIdQuery request, CancellationToken cancellationToken)
    {
        var vehicleModelId = VehicleModelId.From(request.Id);
        var vehicleModel = await _vehicleModelRepository.GetByIdAsync(vehicleModelId, cancellationToken);
        if (vehicleModel is null)
        {
            throw new NotFoundException($"Vehicle model with ID '{request.Id}' was not found.");
        }

        return new VehicleModelDto(
            Id: vehicleModel.Id.Value,
            VehicleBrandId: vehicleModel.VehicleBrandId.Value,
            Name: vehicleModel.Name,
            CreatedAt: vehicleModel.CreatedAt);
    }
}
