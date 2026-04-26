namespace GarageFlow.Api.Users.ListUsers;

public sealed record ListUsersResponse(
    IReadOnlyList<UserListItemResponse> Items,
    int TotalCount,
    int Page,
    int PageSize);
