using GarageFlow.Application.Services.UseCases.GetServiceById;
using GarageFlow.SharedKernel.Domain.Exceptions;
using GarageFlow.Domain.Services.Repositories;
using Mediator;

namespace GarageFlow.Application.Services.UseCases.ListServices;

public sealed class ListServicesHandler(
    IServiceRepository serviceRepository) : IRequestHandler<ListServicesQuery, ListServicesResult>
{
    private const int MaxPageSize = 100;

    private readonly IServiceRepository _serviceRepository = serviceRepository ?? throw new ArgumentNullException(nameof(serviceRepository));

    public async ValueTask<ListServicesResult> Handle(ListServicesQuery request, CancellationToken cancellationToken)
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

        var (items, totalCount) = await _serviceRepository.ListAsync(request.Page, request.PageSize, cancellationToken);

        if (totalCount == 0)
        {
            return new ListServicesResult(
                Items: [],
                TotalCount: 0,
                Page: request.Page,
                PageSize: request.PageSize);
        }

        var dtos = items.Select(service => new ServiceDto(
            Id: service.Id.Value,
            Description: service.Description.Value,
            Price: service.Price.Value,
            CreatedAt: service.CreatedAt)).ToList();

        return new ListServicesResult(
            Items: dtos,
            TotalCount: totalCount,
            Page: request.Page,
            PageSize: request.PageSize);
    }
}
