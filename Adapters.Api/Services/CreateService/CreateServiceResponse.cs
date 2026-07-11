namespace GarageFlow.Adapters.Api.Services.CreateService;

public sealed record CreateServiceResponse(
    Guid Id,
    string Description,
    decimal Price,
    DateTime CreatedAt);

