using GarageFlow.SharedKernel.Domain.Exceptions;
using GarageFlow.Application.Services.Ports;
using GarageFlow.Domain.Services.ValueObjects;
using Mediator;

namespace GarageFlow.Application.Services.UseCases.DeleteService;

public sealed class DeleteServiceHandler(
    IServiceRepository serviceRepository) : IRequestHandler<DeleteServiceCommand, Unit>
{
    private readonly IServiceRepository _serviceRepository = serviceRepository ?? throw new ArgumentNullException(nameof(serviceRepository));

    public async ValueTask<Unit> Handle(DeleteServiceCommand request, CancellationToken cancellationToken)
    {
        var serviceId = ServiceId.From(request.Id);
        var service = await _serviceRepository.GetByIdAsync(serviceId, cancellationToken);
        if (service is null)
        {
            throw new NotFoundException($"Service with ID '{request.Id}' was not found.");
        }

        service.Delete();
        _serviceRepository.Remove(service);

        return Unit.Value;
    }
}
