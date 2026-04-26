using GarageFlow.BuildingBlocks.Domain.Exceptions;
using GarageFlow.BuildingBlocks.Persistence;
using GarageFlow.Domain.Customers.Repositories;
using GarageFlow.Domain.Customers.ValueObjects;
using GarageFlow.Domain.Vehicles.Repositories;
using Mediator;

namespace GarageFlow.Application.Customers.DeleteCustomer;

public sealed class DeleteCustomerHandler(
    ICustomerRepository customerRepository,
    IVehicleRepository vehicleRepository,
    IUnitOfWork unitOfWork) : IRequestHandler<DeleteCustomerCommand, Unit>
{
    private readonly ICustomerRepository _customerRepository = customerRepository ?? throw new ArgumentNullException(nameof(customerRepository));
    private readonly IVehicleRepository _vehicleRepository = vehicleRepository ?? throw new ArgumentNullException(nameof(vehicleRepository));
    private readonly IUnitOfWork _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));

    public async ValueTask<Unit> Handle(DeleteCustomerCommand request, CancellationToken cancellationToken)
    {
        var customerId = CustomerId.From(request.Id);
        var customer = await _customerRepository.GetByIdAsync(customerId, cancellationToken);
        if (customer is null)
        {
            throw new NotFoundException($"Customer with ID '{request.Id}' was not found.");
        }

        var hasVehicles = await _vehicleRepository.ExistsByCustomerIdAsync(customerId, cancellationToken);
        if (hasVehicles)
        {
            throw new BusinessRuleViolationException($"Customer with ID '{request.Id}' cannot be deleted because it has related vehicles.");
        }

        await _unitOfWork.BeginTransactionAsync(cancellationToken);

        try
        {
            customer.Delete();
            _customerRepository.Remove(customer);
            await _unitOfWork.CommitTransactionAsync(cancellationToken);

            return Unit.Value;
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            throw;
        }
    }
}
