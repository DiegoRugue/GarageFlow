namespace GarageFlow.Tests.E2E.Support.Contracts.Common;

public sealed record PaginatedResponse<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    int TotalCount);
