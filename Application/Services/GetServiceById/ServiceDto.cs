namespace GarageFlow.Application.Services.GetServiceById;

public sealed record ServiceDto(
    Guid Id,
    string Description,
    decimal Price,
    DateTime CreatedAt);

