using GarageFlow.Application.Customers.Ports;
using GarageFlow.Domain.Customers.Enums;
using GarageFlow.Domain.Customers.ValueObjects;
using GarageFlow.SharedKernel.Domain.Exceptions;
using Mediator;

namespace GarageFlow.Application.Customers.UseCases.ChangeCustomerStatus;

public sealed class ChangeCustomerStatusHandler(
    ICustomerRepository customerRepository)
    : IRequestHandler<ChangeCustomerStatusCommand, ChangeCustomerStatusResult>
{
    private readonly ICustomerRepository _customerRepository = customerRepository
        ?? throw new ArgumentNullException(nameof(customerRepository));

    public async ValueTask<ChangeCustomerStatusResult> Handle(
        ChangeCustomerStatusCommand request,
        CancellationToken cancellationToken)
    {
        var status = request.Status switch
        {
            nameof(CustomerStatus.Active) => CustomerStatus.Active,
            nameof(CustomerStatus.Suspended) => CustomerStatus.Suspended,
            _ => throw new ValidationException($"Customer status '{request.Status}' is invalid.")
        };

        var customerId = CustomerId.From(request.CustomerId);
        var customer = await _customerRepository.GetByIdAsync(customerId, cancellationToken);
        if (customer is null)
        {
            throw new NotFoundException($"Customer with ID '{request.CustomerId}' was not found.");
        }

        customer.ChangeStatus(status);

        return new ChangeCustomerStatusResult(
            Id: customer.Id.Value,
            Status: customer.Status.ToString());
    }
}
