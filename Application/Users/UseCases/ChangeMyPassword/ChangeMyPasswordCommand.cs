using GarageFlow.Application.Common.Messaging;

namespace GarageFlow.Application.Users.UseCases.ChangeMyPassword;

public sealed record ChangeMyPasswordCommand(
    Guid UserId,
    string CurrentPassword,
    string NewPassword) : ICommand;
