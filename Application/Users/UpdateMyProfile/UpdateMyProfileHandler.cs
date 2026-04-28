using GarageFlow.BuildingBlocks.Domain.Exceptions;
using GarageFlow.BuildingBlocks.Domain.ValueObjects;
using GarageFlow.BuildingBlocks.Persistence;
using GarageFlow.Domain.Users.Repositories;
using GarageFlow.Domain.Users.ValueObjects;
using Mediator;

namespace GarageFlow.Application.Users.UpdateMyProfile;

public sealed class UpdateMyProfileHandler(
    IUserRepository userRepository,
    IUnitOfWork unitOfWork) : IRequestHandler<UpdateMyProfileCommand, UpdateMyProfileResult>
{
    private readonly IUserRepository _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
    private readonly IUnitOfWork _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));

    public async ValueTask<UpdateMyProfileResult> Handle(UpdateMyProfileCommand request, CancellationToken cancellationToken)
    {
        var fullName = FullName.Create(request.FullName);
        var email = Email.Create(request.Email);
        var normalizedEmail = Email.Create(email.Value.ToLowerInvariant());
        var birthDate = UserBirthDate.Create(request.BirthDate);

        var userId = UserId.From(request.UserId);
        var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
        if (user is null)
        {
            throw new NotFoundException($"User with ID '{request.UserId}' was not found.");
        }

        var userWithSameEmail = await _userRepository.GetByEmailAsync(normalizedEmail, cancellationToken);
        if (userWithSameEmail is not null && userWithSameEmail.Id != user.Id)
        {
            throw new BusinessRuleViolationException($"A user with email '{normalizedEmail.Value}' already exists.");
        }

        await _unitOfWork.BeginTransactionAsync(cancellationToken);

        try
        {
            user.UpdateProfile(fullName, normalizedEmail, birthDate);
            await _unitOfWork.CommitTransactionAsync(cancellationToken);

            return new UpdateMyProfileResult(
                Id: user.Id.Value,
                FullName: user.FullName.Value,
                Email: user.Email.Value,
                BirthDate: user.BirthDate.Value,
                Role: user.Role,
                MustChangePassword: user.MustChangePassword,
                CreatedAt: user.CreatedAt,
                UpdatedAt: user.UpdatedAt);
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            throw;
        }
    }
}
