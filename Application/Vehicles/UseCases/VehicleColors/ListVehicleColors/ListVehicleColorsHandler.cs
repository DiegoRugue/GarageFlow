using GarageFlow.Application.Vehicles.UseCases.VehicleColors.GetVehicleColorById;
using GarageFlow.SharedKernel.Domain.Exceptions;
using GarageFlow.Application.Vehicles.Ports;
using Mediator;

namespace GarageFlow.Application.Vehicles.UseCases.VehicleColors.ListVehicleColors;

public sealed class ListVehicleColorsHandler(
    IVehicleColorRepository vehicleColorRepository) : IRequestHandler<ListVehicleColorsQuery, ListVehicleColorsResult>
{
    private const int MaxPageSize = 100;

    private readonly IVehicleColorRepository _vehicleColorRepository = vehicleColorRepository ?? throw new ArgumentNullException(nameof(vehicleColorRepository));

    public async ValueTask<ListVehicleColorsResult> Handle(ListVehicleColorsQuery request, CancellationToken cancellationToken)
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

        var (items, totalCount) = await _vehicleColorRepository.ListAsync(request.Page, request.PageSize, cancellationToken);

        if (totalCount == 0)
        {
            return new ListVehicleColorsResult(
                Items: [],
                TotalCount: 0,
                Page: request.Page,
                PageSize: request.PageSize);
        }

        var dtos = items.Select(vehicleColor => new VehicleColorDto(
            Id: vehicleColor.Id.Value,
            Name: vehicleColor.Name,
            CreatedAt: vehicleColor.CreatedAt)).ToList();

        return new ListVehicleColorsResult(
            Items: dtos,
            TotalCount: totalCount,
            Page: request.Page,
            PageSize: request.PageSize);
    }
}
