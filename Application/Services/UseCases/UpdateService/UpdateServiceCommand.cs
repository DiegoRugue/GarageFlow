using GarageFlow.Application.Common.Messaging;

namespace GarageFlow.Application.Services.UseCases.UpdateService;

public sealed record UpdateServiceCommand(
    Guid Id,
    string Description,
    decimal Price) : ICommand<UpdateServiceResult>;

