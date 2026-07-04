using GarageFlow.Application.Vehicles.UseCases.VehicleModels.GetVehicleModelById;
using GarageFlow.SharedKernel.Domain.Exceptions;
using GarageFlow.Application.Vehicles.Ports;
using GarageFlow.Domain.Vehicles.ValueObjects;
using Mediator;

namespace GarageFlow.Application.Vehicles.UseCases.VehicleModels.ListVehicleModels;

public sealed class ListVehicleModelsHandler(
    IVehicleModelRepository vehicleModelRepository) : IRequestHandler<ListVehicleModelsQuery, ListVehicleModelsResult>
{
    private const int MaxPageSize = 100;

    private readonly IVehicleModelRepository _vehicleModelRepository = vehicleModelRepository ?? throw new ArgumentNullException(nameof(vehicleModelRepository));

    public async ValueTask<ListVehicleModelsResult> Handle(ListVehicleModelsQuery request, CancellationToken cancellationToken)
    {
        if (request.Page < 1)
        {
            throw new ValidationException($"Page must be greater than or equal to 1. Received: {request.Page}.");
        }

        if (request.PageSize < 1)
        {
            throw new ValidationException($"PageSize must be greater than or equal to 1. Received: {request.PageSize}.");
        }

        if (request.PageSize > MaxPageSize)
        {
            throw new ValidationException($"PageSize cannot exceed {MaxPageSize}. Received: {request.PageSize}.");
        }

        if (request.VehicleBrandId.HasValue && request.VehicleBrandId.Value == Guid.Empty)
        {
            throw new ValidationException("VehicleBrandId cannot be empty.");
        }

        var (items, totalCount) = request.VehicleBrandId is null
            ? await _vehicleModelRepository.ListAsync(request.Page, request.PageSize, cancellationToken)
            : await _vehicleModelRepository.ListByVehicleBrandIdAsync(
                VehicleBrandId.From(request.VehicleBrandId.Value),
                request.Page,
                request.PageSize,
                cancellationToken);

        if (totalCount == 0)
        {
            return new ListVehicleModelsResult(
                Items: [],
                TotalCount: 0,
                Page: request.Page,
                PageSize: request.PageSize);
        }

        var dtos = items.Select(vehicleModel => new VehicleModelDto(
            Id: vehicleModel.Id.Value,
            VehicleBrandId: vehicleModel.VehicleBrandId.Value,
            Name: vehicleModel.Name,
            CreatedAt: vehicleModel.CreatedAt)).ToList();

        return new ListVehicleModelsResult(
            Items: dtos,
            TotalCount: totalCount,
            Page: request.Page,
            PageSize: request.PageSize);
    }
}
