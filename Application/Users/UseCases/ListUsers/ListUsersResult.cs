namespace GarageFlow.Application.Users.UseCases.ListUsers;

public sealed record ListUsersResult(
    IReadOnlyList<UserListItemDto> Items,
    int TotalCount,
    int Page,
    int PageSize);
