using GarageFlow.Application.Customers.UseCases.GetCustomerById;
using GarageFlow.SharedKernel.Domain.Exceptions;
using GarageFlow.Domain.Customers.Repositories;
using Mediator;

namespace GarageFlow.Application.Customers.UseCases.ListCustomers;

public sealed class ListCustomersHandler(
    ICustomerRepository customerRepository) : IRequestHandler<ListCustomersQuery, ListCustomersResult>
{
    private const int MaxPageSize = 100;

    private readonly ICustomerRepository _customerRepository = customerRepository ?? throw new ArgumentNullException(nameof(customerRepository));

    public async ValueTask<ListCustomersResult> Handle(ListCustomersQuery request, CancellationToken cancellationToken)
    {
        if (request.Page < 1)
        {
            throw new ValidationException($"Page must be greater than or equal to 1. Received: {request.Page}.");
        }

        if (request.PageSize < 1)
        {
            throw new ValidationException($"PageSize must be greater than or equal to 1. Received: {request.PageSize}.");
        }

        if (request.PageSize > MaxPageSize)
        {
            throw new ValidationException($"PageSize cannot exceed {MaxPageSize}. Received: {request.PageSize}.");
        }

        var (items, totalCount) = await _customerRepository.ListAsync(request.Page, request.PageSize, cancellationToken);

        if (totalCount == 0)
        {
            return new ListCustomersResult(
                Items: [],
                TotalCount: 0,
                Page: request.Page,
                PageSize: request.PageSize);
        }

        var dtos = items.Select(customer => new CustomerDto(
            Id: customer.Id.Value,
            TaxDocument: customer.TaxDocument.Value,
            TaxDocumentType: customer.TaxDocument.DocumentType.ToString(),
            FullName: customer.FullName.Value,
            Email: customer.Email.Value,
            PhoneNumber: customer.PhoneNumber.Value,
            CreatedAt: customer.CreatedAt)).ToList();

        return new ListCustomersResult(
            Items: dtos,
            TotalCount: totalCount,
            Page: request.Page,
            PageSize: request.PageSize);
    }
}
