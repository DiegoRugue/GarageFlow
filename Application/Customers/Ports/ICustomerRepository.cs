using GarageFlow.SharedKernel.Domain.ValueObjects;
using GarageFlow.Domain.Customers.Entities;
using GarageFlow.Domain.Customers.ValueObjects;

namespace GarageFlow.Application.Customers.Ports;

public interface ICustomerRepository
{
    Task<Customer?> GetByIdAsync(CustomerId id, CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<Customer> Items, int TotalCount)> ListAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task AddAsync(Customer customer, CancellationToken cancellationToken = default);

    Task<bool> ExistsByTaxDocumentAsync(
        TaxDocument taxDocument,
        CancellationToken cancellationToken = default);

    void Remove(Customer customer);
}
