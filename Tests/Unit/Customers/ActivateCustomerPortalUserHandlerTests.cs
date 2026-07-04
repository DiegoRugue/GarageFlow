using GarageFlow.Application.Auth.Abstractions;
using GarageFlow.Application.Customers.UseCases.ActivateCustomerPortalUser;
using GarageFlow.SharedKernel.Domain.Exceptions;
using GarageFlow.SharedKernel.Domain.ValueObjects;
using GarageFlow.SharedKernel.Persistence;
using GarageFlow.Domain.Customers.Entities;
using GarageFlow.Domain.Customers.Repositories;
using GarageFlow.Domain.Customers.ValueObjects;
using GarageFlow.Domain.Users.Entities;
using GarageFlow.Domain.Users.Enums;
using GarageFlow.Domain.Users.Repositories;
using GarageFlow.Domain.Users.ValueObjects;
using GarageFlow.Tests.Shared.Customers;
using GarageFlow.Tests.Shared.Users;
using Moq;

namespace GarageFlow.Tests.Unit.Customers;

public class ActivateCustomerPortalUserHandlerTests
{
    [Fact]
    public async Task ActivateCustomerPortalUser_ShouldCreateCustomerUser_WhenCustomerExists()
    {
        var customer = new CustomerBuilder()
            .WithFullName("Portal Customer")
            .WithEmail("portal.customer@example.com")
            .Build();
        var users = new List<User>();
        var customerRepositoryMock = CreateCustomerRepositoryMock([customer]);
        var userRepositoryMock = CreateUserRepositoryMock(users);
        var passwordHashServiceMock = CreatePasswordHashServiceMock();
        var unitOfWorkMock = CreateUnitOfWorkMock();
        var handler = new ActivateCustomerPortalUserHandler(
            customerRepositoryMock.Object,
            userRepositoryMock.Object,
            passwordHashServiceMock.Object,
            unitOfWorkMock.Object);

        var result = await handler.Handle(
            new ActivateCustomerPortalUserCommand(customer.Id.Value, new DateOnly(1990, 1, 1)),
            CancellationToken.None);

        Assert.Equal(customer.Id.Value, result.CustomerId);
        Assert.Equal("Customer", result.Role);
        Assert.Single(users);
        Assert.Equal(customer.Id, users[0].CustomerId);
        Assert.True(users[0].MustChangePassword);
        unitOfWorkMock.Verify(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        unitOfWorkMock.Verify(x => x.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        unitOfWorkMock.Verify(x => x.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ActivateCustomerPortalUser_ShouldThrowNotFoundException_WhenCustomerDoesNotExist()
    {
        var customerRepositoryMock = CreateCustomerRepositoryMock();
        var userRepositoryMock = CreateUserRepositoryMock();
        var passwordHashServiceMock = CreatePasswordHashServiceMock();
        var unitOfWorkMock = CreateUnitOfWorkMock();
        var handler = new ActivateCustomerPortalUserHandler(
            customerRepositoryMock.Object,
            userRepositoryMock.Object,
            passwordHashServiceMock.Object,
            unitOfWorkMock.Object);

        await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(
            new ActivateCustomerPortalUserCommand(Guid.NewGuid(), new DateOnly(1990, 1, 1)),
            CancellationToken.None).AsTask());

        unitOfWorkMock.Verify(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ActivateCustomerPortalUser_ShouldThrowBusinessRuleViolationException_WhenCustomerAlreadyLinked()
    {
        var customer = new CustomerBuilder()
            .WithEmail("linked.customer@example.com")
            .Build();
        var users = new List<User>
        {
            new UserBuilder()
                .WithEmail("other.user@example.com")
                .WithRole(UserRole.Customer)
                .WithCustomerId(customer.Id)
                .Build()
        };
        var customerRepositoryMock = CreateCustomerRepositoryMock([customer]);
        var userRepositoryMock = CreateUserRepositoryMock(users);
        var passwordHashServiceMock = CreatePasswordHashServiceMock();
        var unitOfWorkMock = CreateUnitOfWorkMock();
        var handler = new ActivateCustomerPortalUserHandler(
            customerRepositoryMock.Object,
            userRepositoryMock.Object,
            passwordHashServiceMock.Object,
            unitOfWorkMock.Object);

        await Assert.ThrowsAsync<BusinessRuleViolationException>(() => handler.Handle(
            new ActivateCustomerPortalUserCommand(customer.Id.Value, new DateOnly(1990, 1, 1)),
            CancellationToken.None).AsTask());

        unitOfWorkMock.Verify(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ActivateCustomerPortalUser_ShouldThrowBusinessRuleViolationException_WhenEmailAlreadyUsed()
    {
        var customer = new CustomerBuilder()
            .WithEmail("duplicated.email@example.com")
            .Build();
        var users = new List<User>
        {
            new UserBuilder()
                .WithEmail("duplicated.email@example.com")
                .Build()
        };
        var customerRepositoryMock = CreateCustomerRepositoryMock([customer]);
        var userRepositoryMock = CreateUserRepositoryMock(users);
        var passwordHashServiceMock = CreatePasswordHashServiceMock();
        var unitOfWorkMock = CreateUnitOfWorkMock();
        var handler = new ActivateCustomerPortalUserHandler(
            customerRepositoryMock.Object,
            userRepositoryMock.Object,
            passwordHashServiceMock.Object,
            unitOfWorkMock.Object);

        await Assert.ThrowsAsync<BusinessRuleViolationException>(() => handler.Handle(
            new ActivateCustomerPortalUserCommand(customer.Id.Value, new DateOnly(1990, 1, 1)),
            CancellationToken.None).AsTask());

        unitOfWorkMock.Verify(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ActivateCustomerPortalUser_ShouldThrowValidationException_WhenBirthDateIsInvalid_BeforeDuplicateChecks()
    {
        var customer = new CustomerBuilder()
            .WithEmail("duplicate.check.customer@example.com")
            .Build();
        var users = new List<User>
        {
            new UserBuilder()
                .WithEmail("duplicate.check.customer@example.com")
                .WithRole(UserRole.Customer)
                .WithCustomerId(customer.Id)
                .Build()
        };
        var customerRepositoryMock = CreateCustomerRepositoryMock([customer]);
        var userRepositoryMock = CreateUserRepositoryMock(users);
        var passwordHashServiceMock = CreatePasswordHashServiceMock();
        var unitOfWorkMock = CreateUnitOfWorkMock();
        var handler = new ActivateCustomerPortalUserHandler(
            customerRepositoryMock.Object,
            userRepositoryMock.Object,
            passwordHashServiceMock.Object,
            unitOfWorkMock.Object);

        var invalidFutureBirthDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1);

        await Assert.ThrowsAsync<ValidationException>(() => handler.Handle(
            new ActivateCustomerPortalUserCommand(customer.Id.Value, invalidFutureBirthDate),
            CancellationToken.None).AsTask());

        unitOfWorkMock.Verify(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    private static Mock<IUserRepository> CreateUserRepositoryMock(List<User>? initialUsers = null)
    {
        var users = initialUsers ?? [];
        var repositoryMock = new Mock<IUserRepository>();

        repositoryMock
            .Setup(x => x.GetByIdAsync(It.IsAny<UserId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserId id, CancellationToken _) => users.FirstOrDefault(user => user.Id == id));

        repositoryMock
            .Setup(x => x.GetByEmailAsync(It.IsAny<Email>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Email email, CancellationToken _) =>
                users.FirstOrDefault(user =>
                    string.Equals(user.Email.Value, email.Value, StringComparison.OrdinalIgnoreCase)));

        repositoryMock
            .Setup(x => x.ListAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((int page, int pageSize, CancellationToken _) =>
            {
                var totalCount = users.Count;
                var items = users
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToList();

                return ((IReadOnlyList<User>)items, totalCount);
            });

        repositoryMock
            .Setup(x => x.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .Returns((User user, CancellationToken _) =>
            {
                users.Add(user);
                return Task.CompletedTask;
            });

        repositoryMock
            .Setup(x => x.ExistsByEmailAsync(It.IsAny<Email>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Email email, CancellationToken _) =>
                users.Any(user =>
                    string.Equals(user.Email.Value, email.Value, StringComparison.OrdinalIgnoreCase)));

        repositoryMock
            .Setup(x => x.GetByCustomerIdAsync(It.IsAny<CustomerId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CustomerId customerId, CancellationToken _) =>
                users.FirstOrDefault(user => user.CustomerId == customerId));

        repositoryMock
            .Setup(x => x.ExistsByCustomerIdAsync(It.IsAny<CustomerId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CustomerId customerId, CancellationToken _) =>
                users.Any(user => user.CustomerId == customerId));

        repositoryMock
            .Setup(x => x.Remove(It.IsAny<User>()))
            .Callback((User user) => users.RemoveAll(existing => existing.Id == user.Id));

        return repositoryMock;
    }

    private static Mock<ICustomerRepository> CreateCustomerRepositoryMock(List<Customer>? initialCustomers = null)
    {
        var customers = initialCustomers ?? [];
        var repositoryMock = new Mock<ICustomerRepository>();

        repositoryMock
            .Setup(x => x.GetByIdAsync(It.IsAny<CustomerId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CustomerId customerId, CancellationToken _) =>
                customers.FirstOrDefault(customer => customer.Id == customerId));

        return repositoryMock;
    }

    private static Mock<IPasswordHashService> CreatePasswordHashServiceMock()
    {
        var passwordHashServiceMock = new Mock<IPasswordHashService>();
        passwordHashServiceMock
            .Setup(x => x.Hash(It.IsAny<string>()))
            .Returns("hashed-password");
        return passwordHashServiceMock;
    }

    private static Mock<IUnitOfWork> CreateUnitOfWorkMock()
    {
        var unitOfWorkMock = new Mock<IUnitOfWork>();

        unitOfWorkMock
            .Setup(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        unitOfWorkMock
            .Setup(x => x.CommitTransactionAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        unitOfWorkMock
            .Setup(x => x.RollbackTransactionAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        unitOfWorkMock
            .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        return unitOfWorkMock;
    }
}
