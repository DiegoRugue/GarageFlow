using GarageFlow.Application.Auth.Abstractions;
using GarageFlow.SharedKernel.Domain.Exceptions;
using GarageFlow.SharedKernel.Domain.ValueObjects;
using GarageFlow.SharedKernel.Persistence;
using GarageFlow.Application.Customers.Ports;
using GarageFlow.Domain.Customers.ValueObjects;
using GarageFlow.Domain.Users.Entities;
using GarageFlow.Domain.Users.Enums;
using GarageFlow.Application.Users.Ports;
using GarageFlow.Domain.Users.ValueObjects;
using Mediator;

namespace GarageFlow.Application.Customers.UseCases.ActivateCustomerPortalUser;

public sealed class ActivateCustomerPortalUserHandler(
    ICustomerRepository customerRepository,
    IUserRepository userRepository,
    IPasswordHashService passwordHashService,
    IUnitOfWork unitOfWork) : IRequestHandler<ActivateCustomerPortalUserCommand, ActivateCustomerPortalUserResult>
{
    private readonly ICustomerRepository _customerRepository = customerRepository ?? throw new ArgumentNullException(nameof(customerRepository));
    private readonly IUserRepository _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
    private readonly IPasswordHashService _passwordHashService = passwordHashService ?? throw new ArgumentNullException(nameof(passwordHashService));
    private readonly IUnitOfWork _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));

    public async ValueTask<ActivateCustomerPortalUserResult> Handle(
        ActivateCustomerPortalUserCommand request,
        CancellationToken cancellationToken)
    {
        var customerId = CustomerId.From(request.CustomerId);
        var birthDate = UserBirthDate.Create(request.BirthDate);
        var customer = await _customerRepository.GetByIdAsync(customerId, cancellationToken);
        if (customer is null)
        {
            throw new NotFoundException($"Customer with ID '{request.CustomerId}' was not found.");
        }

        if (await _userRepository.ExistsByCustomerIdAsync(customerId, cancellationToken))
        {
            throw new BusinessRuleViolationException($"Customer with ID '{request.CustomerId}' already has a portal user.");
        }

        var normalizedEmail = Email.Create(customer.Email.Value.ToLowerInvariant());
        if (await _userRepository.ExistsByEmailAsync(normalizedEmail, cancellationToken))
        {
            throw new BusinessRuleViolationException($"A user with email '{normalizedEmail.Value}' already exists.");
        }

        var initialPassword = User.GenerateInitialPassword(customer.FullName, birthDate);
        var passwordHash = _passwordHashService.Hash(initialPassword);

        await _unitOfWork.BeginTransactionAsync(cancellationToken);

        try
        {
            var user = User.Create(
                customer.FullName,
                normalizedEmail,
                birthDate,
                UserRole.Customer,
                passwordHash,
                customerId);

            await _userRepository.AddAsync(user, cancellationToken);
            await _unitOfWork.CommitTransactionAsync(cancellationToken);

            return new ActivateCustomerPortalUserResult(
                user.Id.Value,
                customer.Id.Value,
                user.FullName.Value,
                user.Email.Value,
                user.BirthDate.Value,
                user.Role.ToString(),
                user.MustChangePassword,
                user.CreatedAt);
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            throw;
        }
    }
}
