using GarageFlow.Application.Auth.Abstractions;
using GarageFlow.Application.Customers.Ports;
using GarageFlow.Application.Users.Ports;
using GarageFlow.Domain.Customers.Enums;
using GarageFlow.Domain.Users.Enums;
using GarageFlow.SharedKernel.Domain.Exceptions;
using GarageFlow.SharedKernel.Domain.ValueObjects;
using Mediator;

namespace GarageFlow.Application.Auth.UseCases.VerifyCustomerCredentials;

public sealed class VerifyCustomerCredentialsHandler(
    ICustomerRepository customerRepository,
    IUserRepository userRepository,
    IPasswordHashService passwordHashService)
    : IRequestHandler<VerifyCustomerCredentialsQuery, VerifyCustomerCredentialsResult>
{
    private readonly ICustomerRepository _customerRepository = customerRepository
        ?? throw new ArgumentNullException(nameof(customerRepository));
    private readonly IUserRepository _userRepository = userRepository
        ?? throw new ArgumentNullException(nameof(userRepository));
    private readonly IPasswordHashService _passwordHashService = passwordHashService
        ?? throw new ArgumentNullException(nameof(passwordHashService));

    public async ValueTask<VerifyCustomerCredentialsResult> Handle(
        VerifyCustomerCredentialsQuery request, CancellationToken cancellationToken)
    {
        var cpf = TaxDocument.Create(request.Cpf);
        if (cpf.DocumentType != TaxDocumentType.Cpf)
        {
            throw new ValidationException("A valid CPF is required.");
        }
        if (string.IsNullOrWhiteSpace(request.Password))
        {
            throw new ValidationException("Password is required.");
        }

        var customer = await _customerRepository.GetByTaxDocumentAsync(cpf, cancellationToken);
        var user = customer is null ? null :
            await _userRepository.GetByCustomerIdAsync(customer.Id, cancellationToken);
        var passwordMatches = _passwordHashService.Verify(request.Password, user?.PasswordHash);

        if (customer is null || user is null
            || customer.Status != CustomerStatus.Active
            || user.Role != UserRole.Customer
            || user.CustomerId != customer.Id
            || !passwordMatches)
        {
            throw new UnauthorizedAccessException("invalid_credentials");
        }

        return new VerifyCustomerCredentialsResult(
            user.Id.Value, customer.Id.Value, nameof(UserRole.Customer), user.MustChangePassword);
    }
}
