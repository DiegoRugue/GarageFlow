namespace GarageFlow.Api.Services.GetServiceById;

public sealed record ServiceResponse(
    Guid Id,
    string Description,
    decimal Price,
    DateTime CreatedAt);

