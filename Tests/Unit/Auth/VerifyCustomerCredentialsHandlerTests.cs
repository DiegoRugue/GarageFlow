using GarageFlow.Application.Auth.Abstractions;
using GarageFlow.Application.Auth.UseCases.VerifyCustomerCredentials;
using GarageFlow.Application.Customers.Ports;
using GarageFlow.Application.Users.Ports;
using GarageFlow.Domain.Customers.Entities;
using GarageFlow.Domain.Customers.Enums;
using GarageFlow.Domain.Users.Entities;
using GarageFlow.Domain.Users.Enums;
using GarageFlow.SharedKernel.Domain.Exceptions;
using GarageFlow.SharedKernel.Domain.ValueObjects;
using GarageFlow.Tests.Shared.Customers;
using GarageFlow.Tests.Shared.Users;
using Moq;

namespace GarageFlow.Tests.Unit.Auth;

public sealed class VerifyCustomerCredentialsHandlerTests
{
    private const string Cpf = "52998224725";
    private const string Password = "candidate-password";
    private const string StoredHash = "stored-password-hash";

    [Theory]
    [InlineData(Cpf, false)]
    [InlineData("529.982.247-25", true)]
    public async Task ValidCredentials_ReturnLinkedPortalIdentity(string cpf, bool temporaryPassword)
    {
        var customer = new CustomerBuilder().Build();
        var user = PortalUser(customer, temporaryPassword);
        var (handler, passwords) = CreateHandler(customer, user, passwordMatches: true);

        var result = await handler.Handle(new VerifyCustomerCredentialsQuery(cpf, Password), CancellationToken.None);

        Assert.Equal(user.Id.Value, result.UserId);
        Assert.Equal(customer.Id.Value, result.CustomerId);
        Assert.Equal("Customer", result.Role);
        Assert.Equal(temporaryPassword, result.MustChangePassword);
        passwords.Verify(service => service.Verify(Password, StoredHash), Times.Once);
        passwords.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData("missing-customer")]
    [InlineData("missing-user")]
    [InlineData("suspended")]
    [InlineData("wrong-password")]
    [InlineData("staff-user")]
    [InlineData("wrong-link")]
    public async Task InvalidCredentials_PerformOneHashCheckAndReturnGenericError(string scenario)
    {
        Customer? customer = scenario == "missing-customer" ? null : new CustomerBuilder().Build();
        if (scenario == "suspended")
        {
            customer!.ChangeStatus(CustomerStatus.Suspended);
        }
        User? user = customer is null || scenario == "missing-user" ? null : PortalUser(customer, false);
        if (scenario == "staff-user")
        {
            user = new UserBuilder().WithPasswordHash(StoredHash).Build();
        }
        if (scenario == "wrong-link")
        {
            user = PortalUser(new CustomerBuilder().Build(), false);
        }
        var (handler, passwords) = CreateHandler(customer, user, scenario != "wrong-password");

        var error = await Assert.ThrowsAsync<UnauthorizedAccessException>(async () =>
            await handler.Handle(new VerifyCustomerCredentialsQuery(Cpf, Password), CancellationToken.None));

        Assert.Equal("invalid_credentials", error.Message);
        passwords.Verify(service => service.Verify(Password, user == null ? null : StoredHash), Times.Once);
        passwords.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(null, Password)]
    [InlineData("", Password)]
    [InlineData("12345678900", Password)]
    [InlineData("11222333000181", Password)]
    [InlineData(Cpf, null)]
    [InlineData(Cpf, "")]
    [InlineData(Cpf, " ")]
    public async Task InvalidInput_DoesNotQueryOrCheckPasswords(string? cpf, string? password)
    {
        var customers = new Mock<ICustomerRepository>(MockBehavior.Strict);
        var users = new Mock<IUserRepository>(MockBehavior.Strict);
        var passwords = new Mock<IPasswordHashService>(MockBehavior.Strict);
        var handler = new VerifyCustomerCredentialsHandler(customers.Object, users.Object, passwords.Object);

        await Assert.ThrowsAsync<ValidationException>(async () =>
            await handler.Handle(new VerifyCustomerCredentialsQuery(cpf!, password!), CancellationToken.None));

        customers.VerifyNoOtherCalls();
        users.VerifyNoOtherCalls();
        passwords.VerifyNoOtherCalls();
    }

    private static User PortalUser(Customer customer, bool temporaryPassword) =>
        new UserBuilder().WithRole(UserRole.Customer).WithCustomerId(customer.Id)
            .WithPasswordHash(StoredHash).WithMustChangePassword(temporaryPassword).Build();

    private static (VerifyCustomerCredentialsHandler Handler, Mock<IPasswordHashService> Passwords) CreateHandler(
        Customer? customer, User? user, bool passwordMatches)
    {
        var customers = new Mock<ICustomerRepository>(MockBehavior.Strict);
        customers.Setup(repository => repository.GetByTaxDocumentAsync(
            It.Is<TaxDocument>(document => document.Value == Cpf), CancellationToken.None)).ReturnsAsync(customer);
        var users = new Mock<IUserRepository>(MockBehavior.Strict);
        if (customer is not null)
        {
            users.Setup(repository => repository.GetByCustomerIdAsync(customer.Id, CancellationToken.None)).ReturnsAsync(user);
        }
        var passwords = new Mock<IPasswordHashService>(MockBehavior.Strict);
        passwords.Setup(service => service.Verify(Password, user == null ? null : StoredHash)).Returns(passwordMatches);
        return (new VerifyCustomerCredentialsHandler(customers.Object, users.Object, passwords.Object), passwords);
    }
}
