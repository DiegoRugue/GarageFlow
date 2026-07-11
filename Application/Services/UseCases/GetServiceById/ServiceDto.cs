namespace GarageFlow.Application.Services.UseCases.GetServiceById;

public sealed record ServiceDto(
    Guid Id,
    string Description,
    decimal Price,
    DateTime CreatedAt);

