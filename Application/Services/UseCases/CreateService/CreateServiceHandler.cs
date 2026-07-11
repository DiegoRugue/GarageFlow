using GarageFlow.SharedKernel.Domain.ValueObjects;
using GarageFlow.Domain.Services.Entities;
using GarageFlow.Application.Services.Ports;
using Mediator;

namespace GarageFlow.Application.Services.UseCases.CreateService;

public sealed class CreateServiceHandler(
    IServiceRepository serviceRepository) : IRequestHandler<CreateServiceCommand, CreateServiceResult>
{
    private readonly IServiceRepository _serviceRepository = serviceRepository ?? throw new ArgumentNullException(nameof(serviceRepository));

    public async ValueTask<CreateServiceResult> Handle(CreateServiceCommand request, CancellationToken cancellationToken)
    {
        var description = Description.Create(request.Description);
        var price = Price.Create(request.Price);

        var service = Service.Create(description, price);
        await _serviceRepository.AddAsync(service, cancellationToken);

        return new CreateServiceResult(
            Id: service.Id.Value,
            Description: service.Description.Value,
            Price: service.Price.Value,
            CreatedAt: service.CreatedAt);
    }
}
