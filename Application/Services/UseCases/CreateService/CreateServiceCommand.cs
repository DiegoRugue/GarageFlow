using GarageFlow.Application.Common.Messaging;

namespace GarageFlow.Application.Services.UseCases.CreateService;

public sealed record CreateServiceCommand(
    string Description,
    decimal Price) : ICommand<CreateServiceResult>;

