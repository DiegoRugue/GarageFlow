using GarageFlow.Application.Vehicles.VehicleBrands.GetVehicleBrandById;
using GarageFlow.BuildingBlocks.Domain.Exceptions;
using GarageFlow.Domain.Vehicles.Repositories;
using Mediator;

namespace GarageFlow.Application.Vehicles.VehicleBrands.ListVehicleBrands;

public sealed class ListVehicleBrandsHandler(
    IVehicleBrandRepository vehicleBrandRepository) : IRequestHandler<ListVehicleBrandsQuery, ListVehicleBrandsResult>
{
    private const int MaxPageSize = 100;

    private readonly IVehicleBrandRepository _vehicleBrandRepository = vehicleBrandRepository ?? throw new ArgumentNullException(nameof(vehicleBrandRepository));

    public async ValueTask<ListVehicleBrandsResult> Handle(ListVehicleBrandsQuery request, CancellationToken cancellationToken)
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

        var (items, totalCount) = await _vehicleBrandRepository.ListAsync(request.Page, request.PageSize, cancellationToken);

        if (totalCount == 0)
        {
            return new ListVehicleBrandsResult(
                Items: [],
                TotalCount: 0,
                Page: request.Page,
                PageSize: request.PageSize);
        }

        var dtos = items.Select(vehicleBrand => new VehicleBrandDto(
            Id: vehicleBrand.Id.Value,
            Name: vehicleBrand.Name,
            CreatedAt: vehicleBrand.CreatedAt)).ToList();

        return new ListVehicleBrandsResult(
            Items: dtos,
            TotalCount: totalCount,
            Page: request.Page,
            PageSize: request.PageSize);
    }
}
