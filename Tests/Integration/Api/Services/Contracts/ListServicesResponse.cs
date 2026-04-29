namespace GarageFlow.Tests.Integration.Api.Services.Contracts;

public sealed record ListServicesResponse(
    IReadOnlyList<ServiceResponse> Items,
    int TotalCount,
    int Page,
    int PageSize);
