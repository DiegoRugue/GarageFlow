namespace GarageFlow.Application.Services.UseCases.UpdateService;

public sealed record UpdateServiceResult(
    Guid Id,
    string Description,
    decimal Price,
    DateTime CreatedAt);

