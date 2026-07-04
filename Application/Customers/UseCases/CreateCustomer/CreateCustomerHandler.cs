using GarageFlow.SharedKernel.Domain.Exceptions;
using GarageFlow.SharedKernel.Domain.ValueObjects;
using GarageFlow.SharedKernel.Persistence;
using GarageFlow.Domain.Customers.Entities;
using GarageFlow.Application.Customers.Ports;
using Mediator;

namespace GarageFlow.Application.Customers.UseCases.CreateCustomer;

public sealed class CreateCustomerHandler(
    ICustomerRepository customerRepository,
    IUnitOfWork unitOfWork) : IRequestHandler<CreateCustomerCommand, CreateCustomerResult>
{
    private readonly ICustomerRepository _customerRepository = customerRepository ?? throw new ArgumentNullException(nameof(customerRepository));
    private readonly IUnitOfWork _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));

    public async ValueTask<CreateCustomerResult> Handle(CreateCustomerCommand request, CancellationToken cancellationToken)
    {
        var taxDocument = TaxDocument.Create(request.TaxDocument);
        var fullName = FullName.Create(request.FullName);
        var email = Email.Create(request.Email);
        var phoneNumber = PhoneNumber.Create(request.PhoneNumber);

        var exists = await _customerRepository.ExistsByTaxDocumentAsync(taxDocument, cancellationToken);
        if (exists)
        {
            throw new BusinessRuleViolationException($"A customer with tax document '{request.TaxDocument}' already exists.");
        }

        await _unitOfWork.BeginTransactionAsync(cancellationToken);

        try
        {
            var customer = Customer.Create(taxDocument, fullName, email, phoneNumber);

            await _customerRepository.AddAsync(customer, cancellationToken);
            await _unitOfWork.CommitTransactionAsync(cancellationToken);

            return new CreateCustomerResult(
                Id: customer.Id.Value,
                TaxDocument: customer.TaxDocument.Value,
                TaxDocumentType: customer.TaxDocument.DocumentType.ToString(),
                FullName: customer.FullName.Value,
                Email: customer.Email.Value,
                PhoneNumber: customer.PhoneNumber.Value,
                CreatedAt: customer.CreatedAt);
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            throw;
        }
    }
}
