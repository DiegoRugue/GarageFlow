using GarageFlow.Domain.Services.Entities;
using GarageFlow.Domain.Services.ValueObjects;

namespace GarageFlow.Application.Services.Ports;

public interface IServiceRepository
{
    Task<Service?> GetByIdAsync(
        ServiceId id,
        CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<Service> Items, int TotalCount)> ListAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task AddAsync(Service service, CancellationToken cancellationToken = default);

    void Remove(Service service);
}

