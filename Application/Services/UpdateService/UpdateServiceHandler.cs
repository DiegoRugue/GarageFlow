using GarageFlow.BuildingBlocks.Domain.Exceptions;
using GarageFlow.BuildingBlocks.Domain.ValueObjects;
using GarageFlow.BuildingBlocks.Persistence;
using GarageFlow.Domain.Services.Repositories;
using GarageFlow.Domain.Services.ValueObjects;
using Mediator;

namespace GarageFlow.Application.Services.UpdateService;

public sealed class UpdateServiceHandler(
    IServiceRepository serviceRepository,
    IUnitOfWork unitOfWork) : IRequestHandler<UpdateServiceCommand, UpdateServiceResult>
{
    private readonly IServiceRepository _serviceRepository = serviceRepository ?? throw new ArgumentNullException(nameof(serviceRepository));
    private readonly IUnitOfWork _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));

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

        await _unitOfWork.BeginTransactionAsync(cancellationToken);

        try
        {
            service.Update(description, price);
            await _unitOfWork.CommitTransactionAsync(cancellationToken);

            return new UpdateServiceResult(
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
