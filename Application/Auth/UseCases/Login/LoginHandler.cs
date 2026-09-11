using GarageFlow.Application.Auth.Abstractions;
using GarageFlow.Application.Customers.Ports;
using GarageFlow.SharedKernel.Domain.Exceptions;
using GarageFlow.SharedKernel.Domain.ValueObjects;
using GarageFlow.Application.Users.Ports;
using GarageFlow.Domain.Customers.Enums;
using GarageFlow.Domain.Users.Enums;
using Mediator;

namespace GarageFlow.Application.Auth.UseCases.Login;

public sealed class LoginHandler(
    IUserRepository userRepository,
    ICustomerRepository customerRepository,
    IPasswordHashService passwordHashService,
    ITokenService tokenService) : IRequestHandler<LoginCommand, LoginResult>
{
    private readonly IUserRepository _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
    private readonly ICustomerRepository _customerRepository = customerRepository ?? throw new ArgumentNullException(nameof(customerRepository));
    private readonly IPasswordHashService _passwordHashService = passwordHashService ?? throw new ArgumentNullException(nameof(passwordHashService));
    private readonly ITokenService _tokenService = tokenService ?? throw new ArgumentNullException(nameof(tokenService));

    public async ValueTask<LoginResult> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Password))
        {
            throw new ValidationException("Password cannot be empty or whitespace.");
        }

        var email = Email.Create(request.Email);
        var normalizedEmail = Email.Create(email.Value.ToLowerInvariant());

        var user = await _userRepository.GetByEmailAsync(normalizedEmail, cancellationToken);
        if (user is null || !_passwordHashService.Verify(request.Password, user.PasswordHash))
        {
            throw new UnauthorizedAccessException("Invalid email or password.");
        }

        if (user.Role == UserRole.Customer)
        {
            var customer = user.CustomerId is null
                ? null
                : await _customerRepository.GetByIdAsync(user.CustomerId.Value, cancellationToken);

            if (customer is null || customer.Status != CustomerStatus.Active)
            {
                throw new UnauthorizedAccessException("Invalid email or password.");
            }
        }

        var token = _tokenService.GenerateToken(user);

        return new LoginResult(
            Token: token,
            MustChangePassword: user.MustChangePassword);
    }
}
