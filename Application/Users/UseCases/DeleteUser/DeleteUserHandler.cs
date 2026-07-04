using GarageFlow.SharedKernel.Domain.Exceptions;
using GarageFlow.Application.Users.Ports;
using GarageFlow.Domain.Users.ValueObjects;
using Mediator;

namespace GarageFlow.Application.Users.UseCases.DeleteUser;

public sealed class DeleteUserHandler(
    IUserRepository userRepository) : IRequestHandler<DeleteUserCommand, Unit>
{
    private readonly IUserRepository _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));

    public async ValueTask<Unit> Handle(DeleteUserCommand request, CancellationToken cancellationToken)
    {
        var userId = UserId.From(request.Id);
        var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
        if (user is null)
        {
            throw new NotFoundException($"User with ID '{request.Id}' was not found.");
        }

        _userRepository.Remove(user);

        return Unit.Value;
    }
}
