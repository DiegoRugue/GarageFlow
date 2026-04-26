namespace GarageFlow.Tests.Integration.Api.Users.Contracts;

public sealed record ListUsersResponse(
    IReadOnlyList<UserListItemResponse> Items,
    int TotalCount,
    int Page,
    int PageSize);
