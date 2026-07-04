using GarageFlow.Application.Common.Messaging;

namespace GarageFlow.Application.Users.UseCases.DeleteUser;

public sealed record DeleteUserCommand(Guid Id) : ICommand;
