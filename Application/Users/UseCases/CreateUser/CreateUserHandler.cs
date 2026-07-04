using GarageFlow.Application.Auth.Abstractions;
using GarageFlow.SharedKernel.Domain.Exceptions;
using GarageFlow.SharedKernel.Domain.ValueObjects;
using GarageFlow.SharedKernel.Persistence;
using GarageFlow.Domain.Users.Entities;
using GarageFlow.Domain.Users.Enums;
using GarageFlow.Application.Users.Ports;
using GarageFlow.Domain.Users.ValueObjects;
using Mediator;

namespace GarageFlow.Application.Users.UseCases.CreateUser;

public sealed class CreateUserHandler(
    IUserRepository userRepository,
    IPasswordHashService passwordHashService,
    IUnitOfWork unitOfWork) : IRequestHandler<CreateUserCommand, CreateUserResult>
{
    private readonly IUserRepository _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
    private readonly IPasswordHashService _passwordHashService = passwordHashService ?? throw new ArgumentNullException(nameof(passwordHashService));
    private readonly IUnitOfWork _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));

    public async ValueTask<CreateUserResult> Handle(CreateUserCommand request, CancellationToken cancellationToken)
    {
        var fullName = FullName.Create(request.FullName);
        var email = Email.Create(request.Email);
        var normalizedEmail = Email.Create(email.Value.ToLowerInvariant());
        var birthDate = UserBirthDate.Create(request.BirthDate);

        if (request.Role == UserRole.Customer)
        {
            throw new ValidationException("Customer users must be created through customer portal activation.");
        }

        var exists = await _userRepository.ExistsByEmailAsync(normalizedEmail, cancellationToken);
        if (exists)
        {
            throw new BusinessRuleViolationException($"A user with email '{normalizedEmail.Value}' already exists.");
        }

        var initialPassword = User.GenerateInitialPassword(fullName, birthDate);
        var passwordHash = _passwordHashService.Hash(initialPassword);

        await _unitOfWork.BeginTransactionAsync(cancellationToken);

        try
        {
            var user = User.Create(
                fullName: fullName,
                email: normalizedEmail,
                birthDate: birthDate,
                role: request.Role,
                passwordHash: passwordHash);

            await _userRepository.AddAsync(user, cancellationToken);
            await _unitOfWork.CommitTransactionAsync(cancellationToken);

            return new CreateUserResult(
                Id: user.Id.Value,
                FullName: user.FullName.Value,
                Email: user.Email.Value,
                BirthDate: user.BirthDate.Value,
                Role: user.Role,
                MustChangePassword: user.MustChangePassword,
                CreatedAt: user.CreatedAt);
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            throw;
        }
    }
}
