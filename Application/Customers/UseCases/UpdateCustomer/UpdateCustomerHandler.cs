using GarageFlow.SharedKernel.Domain.Exceptions;
using GarageFlow.SharedKernel.Domain.ValueObjects;
using GarageFlow.Application.Customers.Ports;
using GarageFlow.Domain.Customers.ValueObjects;
using Mediator;

namespace GarageFlow.Application.Customers.UseCases.UpdateCustomer;

public sealed class UpdateCustomerHandler(
    ICustomerRepository customerRepository) : IRequestHandler<UpdateCustomerCommand, UpdateCustomerResult>
{
    private readonly ICustomerRepository _customerRepository = customerRepository ?? throw new ArgumentNullException(nameof(customerRepository));

    public async ValueTask<UpdateCustomerResult> Handle(UpdateCustomerCommand request, CancellationToken cancellationToken)
    {
        var fullName = FullName.Create(request.FullName);
        var email = Email.Create(request.Email);
        var phoneNumber = PhoneNumber.Create(request.PhoneNumber);

        var customerId = CustomerId.From(request.Id);
        var customer = await _customerRepository.GetByIdAsync(customerId, cancellationToken);
        if (customer is null)
        {
            throw new NotFoundException($"Customer with ID '{request.Id}' was not found.");
        }

        customer.Update(fullName, email, phoneNumber);

        return new UpdateCustomerResult(
            Id: customer.Id.Value,
            TaxDocument: customer.TaxDocument.Value,
            TaxDocumentType: customer.TaxDocument.DocumentType.ToString(),
            FullName: customer.FullName.Value,
            Email: customer.Email.Value,
            PhoneNumber: customer.PhoneNumber.Value,
            CreatedAt: customer.CreatedAt);
    }
}
