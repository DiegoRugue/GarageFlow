namespace GarageFlow.Tests.Integration.Api.Services.Contracts;

public sealed record ServiceResponse(
    Guid Id,
    string Description,
    decimal Price,
    DateTime CreatedAt);

