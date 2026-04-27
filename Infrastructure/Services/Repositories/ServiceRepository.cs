using GarageFlow.Domain.Services.Entities;
using GarageFlow.Domain.Services.Repositories;
using GarageFlow.Domain.Services.ValueObjects;
using GarageFlow.Infrastructure.DataAccess;
using Microsoft.EntityFrameworkCore;

namespace GarageFlow.Infrastructure.Services.Repositories;

public sealed class ServiceRepository(GarageFlowDbContext dbContext) : IServiceRepository
{
    private readonly GarageFlowDbContext _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));

    public async Task<Service?> GetByIdAsync(
        ServiceId id,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.Services.FirstOrDefaultAsync(
            service => service.Id == id,
            cancellationToken);
    }

    public async Task<(IReadOnlyList<Service> Items, int TotalCount)> ListAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Services
            .AsNoTracking()
            .OrderBy(service => service.CreatedAt);

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public Task AddAsync(Service service, CancellationToken cancellationToken = default)
    {
        return _dbContext.Services.AddAsync(service, cancellationToken).AsTask();
    }

    public void Remove(Service service)
    {
        _dbContext.Services.Remove(service);
    }
}

