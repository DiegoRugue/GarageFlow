using GarageFlow.BuildingBlocks.Domain.Exceptions;
using GarageFlow.Domain.Users.Repositories;
using Mediator;

namespace GarageFlow.Application.Users.ListUsers;

public sealed class ListUsersHandler(
    IUserRepository userRepository) : IRequestHandler<ListUsersQuery, ListUsersResult>
{
    private const int MaxPageSize = 100;

    private readonly IUserRepository _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));

    public async ValueTask<ListUsersResult> Handle(ListUsersQuery request, CancellationToken cancellationToken)
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

        var (items, totalCount) = await _userRepository.ListAsync(request.Page, request.PageSize, cancellationToken);

        if (totalCount == 0)
        {
            return new ListUsersResult(
                Items: [],
                TotalCount: 0,
                Page: request.Page,
                PageSize: request.PageSize);
        }

        var dtos = items.Select(user => new UserListItem(
            Id: user.Id.Value,
            FullName: user.FullName.Value,
            Email: user.Email.Value,
            BirthDate: user.BirthDate.Value,
            Role: user.Role,
            MustChangePassword: user.MustChangePassword,
            CreatedAt: user.CreatedAt,
            UpdatedAt: user.UpdatedAt)).ToList();

        return new ListUsersResult(
            Items: dtos,
            TotalCount: totalCount,
            Page: request.Page,
            PageSize: request.PageSize);
    }
}
