using GarageFlow.SharedKernel.Domain.Exceptions;
using GarageFlow.SharedKernel.Domain.ValueObjects;
using GarageFlow.Application.Services.Ports;
using GarageFlow.Domain.Services.ValueObjects;
using Mediator;

namespace GarageFlow.Application.Services.UseCases.UpdateService;

public sealed class UpdateServiceHandler(
    IServiceRepository serviceRepository) : IRequestHandler<UpdateServiceCommand, UpdateServiceResult>
{
    private readonly IServiceRepository _serviceRepository = serviceRepository ?? throw new ArgumentNullException(nameof(serviceRepository));

    public async ValueTask<UpdateServiceResult> Handle(UpdateServiceCommand request, CancellationToken cancellationToken)
    {
        var description = Description.Create(request.Description);
        var price = Price.Create(request.Price);

        var serviceId = ServiceId.From(request.Id);
        var service = await _serviceRepository.GetByIdAsync(serviceId, cancellationToken);
        if (service is null)
        {
            throw new NotFoundException($"Service with ID '{request.Id}' was not found.");
        }

        service.Update(description, price);

        return new UpdateServiceResult(
            Id: service.Id.Value,
            Description: service.Description.Value,
            Price: service.Price.Value,
            CreatedAt: service.CreatedAt);
    }
}
