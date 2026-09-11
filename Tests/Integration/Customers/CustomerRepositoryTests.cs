using GarageFlow.Adapters.Infrastructure.Customers.Repositories;
using GarageFlow.Adapters.Infrastructure.DataAccess;
using GarageFlow.SharedKernel.Domain.ValueObjects;
using GarageFlow.Tests.Shared.Customers;
using Microsoft.EntityFrameworkCore;

namespace GarageFlow.Tests.Integration.Customers;

public sealed class CustomerRepositoryTests
{
    [Fact]
    public async Task GetByTaxDocumentAsync_ShouldReturnDetachedCustomer_ForNormalizedExactMatch()
    {
        await using var dbContext = CreateDbContext();
        var repository = new CustomerRepository(dbContext);
        var customer = new CustomerBuilder()
            .WithTaxDocument("529.982.247-25")
            .Build();

        await repository.AddAsync(customer);
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();

        var result = await repository.GetByTaxDocumentAsync(TaxDocument.Create("52998224725"));

        Assert.NotNull(result);
        Assert.Equal(customer.Id, result.Id);
        Assert.Equal("52998224725", result.TaxDocument.Value);
        Assert.Equal(EntityState.Detached, dbContext.Entry(result).State);
    }

    [Fact]
    public async Task Status_ShouldRoundTripThroughCustomerRepository()
    {
        await using var dbContext = CreateDbContext();
        var repository = new CustomerRepository(dbContext);
        var customer = new CustomerBuilder().Build();
        customer.ChangeStatus(GarageFlow.Domain.Customers.Enums.CustomerStatus.Suspended);

        await repository.AddAsync(customer);
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();

        var result = await repository.GetByIdAsync(customer.Id);

        Assert.NotNull(result);
        Assert.Equal(GarageFlow.Domain.Customers.Enums.CustomerStatus.Suspended, result.Status);
    }

    private static GarageFlowDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<GarageFlowDbContext>()
            .UseInMemoryDatabase($"garageflow-customer-repository-tests-{Guid.NewGuid():N}")
            .Options;

        return new GarageFlowDbContext(options);
    }
}
