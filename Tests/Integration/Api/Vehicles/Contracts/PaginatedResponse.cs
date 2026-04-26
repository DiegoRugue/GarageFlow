namespace GarageFlow.Tests.Integration.Api.Vehicles.Contracts;

public sealed record PaginatedResponse<T>(
    IReadOnlyList<T> Items,
    int TotalCount,
    int Page,
    int PageSize);
