using GarageFlow.SharedKernel.Domain.Exceptions;
using GarageFlow.Domain.Vehicles.Repositories;
using GarageFlow.Domain.Vehicles.ValueObjects;
using Mediator;

namespace GarageFlow.Application.Vehicles.VehicleBrands.GetVehicleBrandById;

public sealed class GetVehicleBrandByIdHandler(
    IVehicleBrandRepository vehicleBrandRepository) : IRequestHandler<GetVehicleBrandByIdQuery, VehicleBrandDto>
{
    private readonly IVehicleBrandRepository _vehicleBrandRepository = vehicleBrandRepository ?? throw new ArgumentNullException(nameof(vehicleBrandRepository));

    public async ValueTask<VehicleBrandDto> Handle(GetVehicleBrandByIdQuery request, CancellationToken cancellationToken)
    {
        var vehicleBrandId = VehicleBrandId.From(request.Id);
        var vehicleBrand = await _vehicleBrandRepository.GetByIdAsync(vehicleBrandId, cancellationToken);

        if (vehicleBrand is null)
        {
            throw new NotFoundException($"Vehicle brand with ID '{request.Id}' was not found.");
        }

        return new VehicleBrandDto(
            Id: vehicleBrand.Id.Value,
            Name: vehicleBrand.Name,
            CreatedAt: vehicleBrand.CreatedAt);
    }
}
