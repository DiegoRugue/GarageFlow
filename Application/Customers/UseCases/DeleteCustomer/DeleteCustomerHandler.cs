using GarageFlow.SharedKernel.Domain.Exceptions;
using GarageFlow.Application.Customers.Ports;
using GarageFlow.Domain.Customers.ValueObjects;
using GarageFlow.Application.Vehicles.Ports;
using Mediator;

namespace GarageFlow.Application.Customers.UseCases.DeleteCustomer;

public sealed class DeleteCustomerHandler(
    ICustomerRepository customerRepository,
    IVehicleRepository vehicleRepository) : IRequestHandler<DeleteCustomerCommand, Unit>
{
    private readonly ICustomerRepository _customerRepository = customerRepository ?? throw new ArgumentNullException(nameof(customerRepository));
    private readonly IVehicleRepository _vehicleRepository = vehicleRepository ?? throw new ArgumentNullException(nameof(vehicleRepository));

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

        customer.Delete();
        _customerRepository.Remove(customer);

        return Unit.Value;
    }
}
