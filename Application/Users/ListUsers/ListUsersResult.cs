namespace GarageFlow.Application.Users.ListUsers;

public sealed record ListUsersResult(
    IReadOnlyList<UserListItem> Items,
    int TotalCount,
    int Page,
    int PageSize);
