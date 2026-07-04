using GarageFlow.Application.Common.Messaging;

namespace GarageFlow.Application.Services.UseCases.DeleteService;

public sealed record DeleteServiceCommand(Guid Id) : ICommand;

