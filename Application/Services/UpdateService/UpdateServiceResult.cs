namespace GarageFlow.Application.Services.UpdateService;

public sealed record UpdateServiceResult(
    Guid Id,
    string Description,
    decimal Price,
    DateTime CreatedAt);

