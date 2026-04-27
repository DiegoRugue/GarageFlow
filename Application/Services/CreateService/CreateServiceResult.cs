namespace GarageFlow.Application.Services.CreateService;

public sealed record CreateServiceResult(
    Guid Id,
    string Description,
    decimal Price,
    DateTime CreatedAt);

