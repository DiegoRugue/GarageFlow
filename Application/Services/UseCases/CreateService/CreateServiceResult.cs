namespace GarageFlow.Application.Services.UseCases.CreateService;

public sealed record CreateServiceResult(
    Guid Id,
    string Description,
    decimal Price,
    DateTime CreatedAt);

