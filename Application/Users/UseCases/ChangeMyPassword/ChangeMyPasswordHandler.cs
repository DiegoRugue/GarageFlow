using GarageFlow.Application.Auth.Abstractions;
using GarageFlow.SharedKernel.Domain.Exceptions;
using GarageFlow.Application.Users.Ports;
using GarageFlow.Domain.Users.ValueObjects;
using Mediator;

namespace GarageFlow.Application.Users.UseCases.ChangeMyPassword;

public sealed class ChangeMyPasswordHandler(
    IUserRepository userRepository,
    IPasswordHashService passwordHashService) : IRequestHandler<ChangeMyPasswordCommand, Unit>
{
    private readonly IUserRepository _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
    private readonly IPasswordHashService _passwordHashService = passwordHashService ?? throw new ArgumentNullException(nameof(passwordHashService));

    public async ValueTask<Unit> Handle(ChangeMyPasswordCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.CurrentPassword))
        {
            throw new ValidationException("Current password cannot be empty or whitespace.");
        }

        if (string.IsNullOrWhiteSpace(request.NewPassword))
        {
            throw new ValidationException("New password cannot be empty or whitespace.");
        }

        var userId = UserId.From(request.UserId);
        var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
        if (user is null)
        {
            throw new NotFoundException($"User with ID '{request.UserId}' was not found.");
        }

        if (!_passwordHashService.Verify(request.CurrentPassword, user.PasswordHash))
        {
            throw new BusinessRuleViolationException("Current password is invalid.");
        }

        var newPasswordHash = _passwordHashService.Hash(request.NewPassword);

        user.ChangePassword(newPasswordHash);

        return Unit.Value;
    }
}
