using GarageFlow.Domain.Customers.Repositories;
using GarageFlow.Domain.Customers.ValueObjects;
using Mediator;

namespace GarageFlow.Application.Customers.GetCustomerById;

public sealed class GetCustomerByIdHandler(
    ICustomerRepository customerRepository) : IRequestHandler<GetCustomerByIdQuery, CustomerDto?>
{
    private readonly ICustomerRepository _customerRepository = customerRepository ?? throw new ArgumentNullException(nameof(customerRepository));

    public async ValueTask<CustomerDto?> Handle(GetCustomerByIdQuery request, CancellationToken cancellationToken)
    {
        var customerId = CustomerId.From(request.Id);
        var customer = await _customerRepository.GetByIdAsync(customerId, cancellationToken);

        if (customer is null)
        {
            return null;
        }

        return new CustomerDto(
            Id: customer.Id.Value,
            TaxDocument: customer.TaxDocument.Value,
            TaxDocumentType: customer.TaxDocument.DocumentType.ToString(),
            FullName: customer.FullName.Value,
            Email: customer.Email.Value,
            PhoneNumber: customer.PhoneNumber.Value,
            CreatedAt: customer.CreatedAt);
    }
}
