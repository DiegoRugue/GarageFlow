using GarageFlow.SharedKernel.Domain.Exceptions;
using GarageFlow.Domain.Services.Repositories;
using GarageFlow.Domain.Services.ValueObjects;
using Mediator;

namespace GarageFlow.Application.Services.UseCases.GetServiceById;

public sealed class GetServiceByIdHandler(
    IServiceRepository serviceRepository) : IRequestHandler<GetServiceByIdQuery, ServiceDto>
{
    private readonly IServiceRepository _serviceRepository = serviceRepository ?? throw new ArgumentNullException(nameof(serviceRepository));

    public async ValueTask<ServiceDto> Handle(GetServiceByIdQuery request, CancellationToken cancellationToken)
    {
        var serviceId = ServiceId.From(request.Id);
        var service = await _serviceRepository.GetByIdAsync(serviceId, cancellationToken);

        if (service is null)
        {
            throw new NotFoundException($"Service with ID '{request.Id}' was not found.");
        }

        return new ServiceDto(
            Id: service.Id.Value,
            Description: service.Description.Value,
            Price: service.Price.Value,
            CreatedAt: service.CreatedAt);
    }
}
