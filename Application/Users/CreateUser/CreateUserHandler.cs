using GarageFlow.Application.Auth.Abstractions;
using GarageFlow.BuildingBlocks.Domain.Exceptions;
using GarageFlow.BuildingBlocks.Domain.ValueObjects;
using GarageFlow.BuildingBlocks.Persistence;
using GarageFlow.Domain.Users.Entities;
using GarageFlow.Domain.Users.Repositories;
using Mediator;

namespace GarageFlow.Application.Users.CreateUser;

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

        var exists = await _userRepository.ExistsByEmailAsync(normalizedEmail, cancellationToken);
        if (exists)
        {
            throw new BusinessRuleViolationException($"A user with email '{normalizedEmail.Value}' already exists.");
        }

        var initialPassword = User.GenerateInitialPassword(fullName, request.BirthDate);
        var passwordHash = _passwordHashService.Hash(initialPassword);

        await _unitOfWork.BeginTransactionAsync(cancellationToken);

        try
        {
            var user = User.Create(
                fullName: fullName,
                email: normalizedEmail,
                birthDate: request.BirthDate,
                role: request.Role,
                passwordHash: passwordHash);

            await _userRepository.AddAsync(user, cancellationToken);
            await _unitOfWork.CommitTransactionAsync(cancellationToken);

            return new CreateUserResult(
                Id: user.Id.Value,
                FullName: user.FullName.Value,
                Email: user.Email.Value,
                BirthDate: user.BirthDate,
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
