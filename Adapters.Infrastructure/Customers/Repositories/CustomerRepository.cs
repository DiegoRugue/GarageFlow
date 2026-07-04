using GarageFlow.SharedKernel.Domain.ValueObjects;
using GarageFlow.Domain.Customers.Entities;
using GarageFlow.Domain.Customers.Repositories;
using GarageFlow.Domain.Customers.ValueObjects;
using GarageFlow.Adapters.Infrastructure.DataAccess;
using Microsoft.EntityFrameworkCore;

namespace GarageFlow.Adapters.Infrastructure.Customers.Repositories;

public sealed class CustomerRepository(GarageFlowDbContext dbContext) : ICustomerRepository
{
    private readonly GarageFlowDbContext _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));

    public async Task<Customer?> GetByIdAsync(CustomerId id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Customers.FirstOrDefaultAsync(
            customer => customer.Id == id,
            cancellationToken);
    }

    public async Task<(IReadOnlyList<Customer> Items, int TotalCount)> ListAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Customers
            .AsNoTracking()
            .OrderBy(customer => customer.CreatedAt);

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public Task AddAsync(Customer customer, CancellationToken cancellationToken = default)
    {
        return _dbContext.Customers.AddAsync(customer, cancellationToken).AsTask();
    }

    public async Task<bool> ExistsByTaxDocumentAsync(
        TaxDocument taxDocument,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.Customers
            .AsNoTracking()
            .AnyAsync(customer => customer.TaxDocument == taxDocument, cancellationToken);
    }

    public void Remove(Customer customer)
    {
        _dbContext.Customers.Remove(customer);
    }
}
