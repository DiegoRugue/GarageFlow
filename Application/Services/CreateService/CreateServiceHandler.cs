using GarageFlow.BuildingBlocks.Persistence;
using GarageFlow.Domain.Services.Entities;
using GarageFlow.Domain.Services.Repositories;
using GarageFlow.Domain.Services.ValueObjects;
using Mediator;

namespace GarageFlow.Application.Services.CreateService;

public sealed class CreateServiceHandler(
    IServiceRepository serviceRepository,
    IUnitOfWork unitOfWork) : IRequestHandler<CreateServiceCommand, CreateServiceResult>
{
    private readonly IServiceRepository _serviceRepository = serviceRepository ?? throw new ArgumentNullException(nameof(serviceRepository));
    private readonly IUnitOfWork _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));

    public async ValueTask<CreateServiceResult> Handle(CreateServiceCommand request, CancellationToken cancellationToken)
    {
        var description = ServiceDescription.Create(request.Description);
        var price = ServicePrice.Create(request.Price);

        await _unitOfWork.BeginTransactionAsync(cancellationToken);

        try
        {
            var service = Service.Create(description, price);
            await _serviceRepository.AddAsync(service, cancellationToken);
            await _unitOfWork.CommitTransactionAsync(cancellationToken);

            return new CreateServiceResult(
                Id: service.Id.Value,
                Description: service.Description.Value,
                Price: service.Price.Value,
                CreatedAt: service.CreatedAt);
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            throw;
        }
    }
}
