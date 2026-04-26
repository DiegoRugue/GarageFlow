using GarageFlow.BuildingBlocks.Domain.ValueObjects;
using GarageFlow.Domain.Users.Entities;
using GarageFlow.Domain.Users.ValueObjects;

namespace GarageFlow.Domain.Users.Repositories;

public interface IUserRepository
{
    Task<User?> GetByIdAsync(UserId id, CancellationToken cancellationToken = default);

    Task<User?> GetByEmailAsync(Email email, CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<User> Items, int TotalCount)> ListAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task AddAsync(User user, CancellationToken cancellationToken = default);

    Task<bool> ExistsByEmailAsync(Email email, CancellationToken cancellationToken = default);

    void Remove(User user);
}
