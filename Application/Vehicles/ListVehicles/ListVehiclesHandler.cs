using GarageFlow.Application.Vehicles.GetVehicleById;
using GarageFlow.SharedKernel.Domain.Exceptions;
using GarageFlow.Domain.Customers.ValueObjects;
using GarageFlow.Domain.Vehicles.Repositories;
using Mediator;

namespace GarageFlow.Application.Vehicles.ListVehicles;

public sealed class ListVehiclesHandler(
    IVehicleRepository vehicleRepository) : IRequestHandler<ListVehiclesQuery, ListVehiclesResult>
{
    private const int MaxPageSize = 100;

    private readonly IVehicleRepository _vehicleRepository = vehicleRepository ?? throw new ArgumentNullException(nameof(vehicleRepository));

    public async ValueTask<ListVehiclesResult> Handle(ListVehiclesQuery request, CancellationToken cancellationToken)
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

        if (request.CustomerId.HasValue && request.CustomerId.Value == Guid.Empty)
        {
            throw new ValidationException("CustomerId cannot be empty.");
        }

        CustomerId? customerId = request.CustomerId.HasValue
            ? CustomerId.From(request.CustomerId.Value)
            : null;

        var (items, totalCount) = await _vehicleRepository.ListDetailsAsync(
            request.Page,
            request.PageSize,
            customerId,
            cancellationToken);

        if (totalCount == 0)
        {
            return new ListVehiclesResult(
                Items: [],
                TotalCount: 0,
                Page: request.Page,
                PageSize: request.PageSize);
        }

        var dtos = items.Select(vehicle => new VehicleDto(
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
            CreatedAt: vehicle.CreatedAt)).ToList();

        return new ListVehiclesResult(
            Items: dtos,
            TotalCount: totalCount,
            Page: request.Page,
            PageSize: request.PageSize);
    }
}
