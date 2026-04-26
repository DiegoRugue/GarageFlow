using Mediator;

namespace GarageFlow.Application.Users.ChangeMyPassword;

public sealed record ChangeMyPasswordCommand(
    Guid UserId,
    string CurrentPassword,
    string NewPassword) : IRequest<Unit>;
