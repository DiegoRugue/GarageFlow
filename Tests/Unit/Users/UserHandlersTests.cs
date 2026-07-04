using GarageFlow.Application.Auth.Abstractions;
using GarageFlow.Application.Auth.Login;
using GarageFlow.Application.Users.ChangeMyPassword;
using GarageFlow.Application.Users.CreateUser;
using GarageFlow.Application.Users.DeleteUser;
using GarageFlow.Application.Users.ListUsers;
using GarageFlow.Application.Users.UpdateMyProfile;
using GarageFlow.SharedKernel.Domain.Exceptions;
using GarageFlow.SharedKernel.Domain.ValueObjects;
using GarageFlow.SharedKernel.Persistence;
using GarageFlow.Domain.Users.Entities;
using GarageFlow.Domain.Users.Enums;
using GarageFlow.Domain.Users.Repositories;
using GarageFlow.Domain.Users.ValueObjects;
using GarageFlow.Tests.Shared.Users;
using Moq;

namespace GarageFlow.Tests.Unit.Users;

public class UserHandlersTests
{
    [Fact]
    public async Task Handle_ShouldCreateUser_WhenDataIsValid()
    {
        var repositoryMock = CreateRepositoryMock();
        var passwordHashServiceMock = new Mock<IPasswordHashService>();
        passwordHashServiceMock
            .Setup(x => x.Hash(It.IsAny<string>()))
            .Returns("hashed-password");
        var unitOfWorkMock = CreateUnitOfWorkMock();
        var handler = new CreateUserHandler(
            repositoryMock.Object,
            passwordHashServiceMock.Object,
            unitOfWorkMock.Object);

        var command = new CreateUserCommand(
            FullName: "Alice Smith",
            Email: " Alice.Smith@Example.com ",
            BirthDate: new DateOnly(1992, 3, 15),
            Role: UserRole.Attendant);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.NotEqual(Guid.Empty, result.Id);
        Assert.Equal("Alice Smith", result.FullName);
        Assert.Equal("alice.smith@example.com", result.Email);
        Assert.Equal(new DateOnly(1992, 3, 15), result.BirthDate);
        Assert.Equal(UserRole.Attendant, result.Role);
        Assert.True(result.MustChangePassword);
        passwordHashServiceMock.Verify(x => x.Hash(It.IsAny<string>()), Times.Once);
        unitOfWorkMock.Verify(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        unitOfWorkMock.Verify(x => x.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        unitOfWorkMock.Verify(x => x.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldRollbackTransaction_WhenCreateUserCommitFails()
    {
        var repositoryMock = CreateRepositoryMock();
        var passwordHashServiceMock = new Mock<IPasswordHashService>();
        passwordHashServiceMock
            .Setup(x => x.Hash(It.IsAny<string>()))
            .Returns("hashed-password");
        var unitOfWorkMock = CreateUnitOfWorkMock();
        unitOfWorkMock
            .Setup(x => x.CommitTransactionAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Commit failed."));
        var handler = new CreateUserHandler(
            repositoryMock.Object,
            passwordHashServiceMock.Object,
            unitOfWorkMock.Object);

        var command = new CreateUserCommand(
            FullName: "Alice Smith",
            Email: "alice.smith@example.com",
            BirthDate: new DateOnly(1992, 3, 15),
            Role: UserRole.Attendant);

        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(command, CancellationToken.None).AsTask());

        unitOfWorkMock.Verify(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        unitOfWorkMock.Verify(x => x.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        unitOfWorkMock.Verify(x => x.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldReturnEmptyResult_WhenListUsersFindsNoData()
    {
        var repositoryMock = CreateRepositoryMock();
        var handler = new ListUsersHandler(repositoryMock.Object);

        var result = await handler.Handle(new ListUsersQuery(Page: 1, PageSize: 10), CancellationToken.None);

        Assert.Empty(result.Items);
        Assert.Equal(0, result.TotalCount);
        Assert.Equal(1, result.Page);
        Assert.Equal(10, result.PageSize);
    }

    [Fact]
    public async Task Handle_ShouldReturnPagedUsers_WhenListUsersHasData()
    {
        var firstUser = new UserBuilder()
            .WithFullName("User One")
            .WithEmail("user.one@example.com")
            .Build();
        var secondUser = new UserBuilder()
            .WithFullName("User Two")
            .WithEmail("user.two@example.com")
            .Build();
        var repositoryMock = CreateRepositoryMock([firstUser, secondUser]);
        var handler = new ListUsersHandler(repositoryMock.Object);

        var result = await handler.Handle(new ListUsersQuery(Page: 1, PageSize: 1), CancellationToken.None);

        var item = Assert.Single(result.Items);
        Assert.Equal(firstUser.Id.Value, item.Id);
        Assert.Equal("User One", item.FullName);
        Assert.Equal("user.one@example.com", item.Email);
        Assert.Equal(2, result.TotalCount);
        Assert.Equal(1, result.Page);
        Assert.Equal(1, result.PageSize);
    }

    [Theory]
    [InlineData(0, 10)]
    [InlineData(1, 0)]
    [InlineData(1, 101)]
    public async Task Handle_ShouldThrowValidationException_WhenListUsersPaginationIsInvalid(int page, int pageSize)
    {
        var repositoryMock = CreateRepositoryMock();
        var handler = new ListUsersHandler(repositoryMock.Object);

        await Assert.ThrowsAsync<ValidationException>(
            () => handler.Handle(new ListUsersQuery(Page: page, PageSize: pageSize), CancellationToken.None).AsTask());
    }

    [Fact]
    public async Task Handle_ShouldDeleteUser_WhenUserExists()
    {
        var user = new UserBuilder().Build();
        var repositoryMock = CreateRepositoryMock([user]);
        var unitOfWorkMock = CreateUnitOfWorkMock();
        var handler = new DeleteUserHandler(repositoryMock.Object, unitOfWorkMock.Object);

        await handler.Handle(new DeleteUserCommand(user.Id.Value), CancellationToken.None);

        unitOfWorkMock.Verify(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        unitOfWorkMock.Verify(x => x.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        unitOfWorkMock.Verify(x => x.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
        repositoryMock.Verify(x => x.Remove(It.Is<User>(existing => existing.Id == user.Id)), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldThrowNotFoundException_WhenDeletingMissingUser()
    {
        var repositoryMock = CreateRepositoryMock();
        var unitOfWorkMock = CreateUnitOfWorkMock();
        var handler = new DeleteUserHandler(repositoryMock.Object, unitOfWorkMock.Object);

        await Assert.ThrowsAsync<NotFoundException>(
            () => handler.Handle(new DeleteUserCommand(Guid.NewGuid()), CancellationToken.None).AsTask());

        unitOfWorkMock.Verify(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
        repositoryMock.Verify(x => x.Remove(It.IsAny<User>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldRollbackTransaction_WhenDeleteUserCommitFails()
    {
        var user = new UserBuilder().Build();
        var repositoryMock = CreateRepositoryMock([user]);
        var unitOfWorkMock = CreateUnitOfWorkMock();
        unitOfWorkMock
            .Setup(x => x.CommitTransactionAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Commit failed."));
        var handler = new DeleteUserHandler(repositoryMock.Object, unitOfWorkMock.Object);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => handler.Handle(new DeleteUserCommand(user.Id.Value), CancellationToken.None).AsTask());

        unitOfWorkMock.Verify(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        unitOfWorkMock.Verify(x => x.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldThrowBusinessRuleViolationException_WhenCreatingUserWithDuplicateEmail()
    {
        var existingUser = new UserBuilder()
            .WithEmail("existing.user@example.com")
            .Build();
        var repositoryMock = CreateRepositoryMock([existingUser]);
        var passwordHashServiceMock = new Mock<IPasswordHashService>();
        var unitOfWorkMock = CreateUnitOfWorkMock();
        var handler = new CreateUserHandler(
            repositoryMock.Object,
            passwordHashServiceMock.Object,
            unitOfWorkMock.Object);

        var command = new CreateUserCommand(
            FullName: "New User",
            Email: " Existing.User@Example.com ",
            BirthDate: new DateOnly(1994, 8, 14),
            Role: UserRole.Attendant);

        await Assert.ThrowsAsync<BusinessRuleViolationException>(
            async () => await handler.Handle(command, CancellationToken.None));

        unitOfWorkMock.Verify(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
        passwordHashServiceMock.Verify(x => x.Hash(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task CreateUser_ShouldThrowValidationException_WhenRoleIsCustomer()
    {
        var userRepositoryMock = CreateRepositoryMock();
        var passwordHashServiceMock = new Mock<IPasswordHashService>();
        var unitOfWorkMock = CreateUnitOfWorkMock();
        var handler = new CreateUserHandler(
            userRepositoryMock.Object,
            passwordHashServiceMock.Object,
            unitOfWorkMock.Object);

        await Assert.ThrowsAsync<ValidationException>(() => handler.Handle(
            new CreateUserCommand(
                FullName: "Customer User",
                Email: "customer.user@example.com",
                BirthDate: new DateOnly(1990, 1, 1),
                Role: UserRole.Customer),
            CancellationToken.None).AsTask());

        unitOfWorkMock.Verify(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldThrowUnauthorizedAccessException_WhenLoginUserDoesNotExist()
    {
        var repositoryMock = CreateRepositoryMock();
        var passwordHashServiceMock = new Mock<IPasswordHashService>();
        var tokenServiceMock = new Mock<ITokenService>();
        var handler = new LoginHandler(
            repositoryMock.Object,
            passwordHashServiceMock.Object,
            tokenServiceMock.Object);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            async () => await handler.Handle(
                new LoginCommand("missing.user@example.com", "any-password"),
                CancellationToken.None));

        tokenServiceMock.Verify(x => x.GenerateToken(It.IsAny<User>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldThrowUnauthorizedAccessException_WhenLoginPasswordIsInvalid()
    {
        var existingUser = new UserBuilder()
            .WithEmail("valid.user@example.com")
            .WithPasswordHash("stored-hash")
            .Build();
        var repositoryMock = CreateRepositoryMock([existingUser]);
        var passwordHashServiceMock = new Mock<IPasswordHashService>();
        passwordHashServiceMock
            .Setup(x => x.Verify("wrong-password", "stored-hash"))
            .Returns(false);
        var tokenServiceMock = new Mock<ITokenService>();
        var handler = new LoginHandler(
            repositoryMock.Object,
            passwordHashServiceMock.Object,
            tokenServiceMock.Object);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            async () => await handler.Handle(
                new LoginCommand("valid.user@example.com", "wrong-password"),
                CancellationToken.None));

        tokenServiceMock.Verify(x => x.GenerateToken(It.IsAny<User>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldUpdateProfile_WhenRequestIsValid()
    {
        var user = new UserBuilder()
            .WithFullName("Original Name")
            .WithEmail("original.user@example.com")
            .WithBirthDate(new DateOnly(1992, 5, 10))
            .WithMustChangePassword(false)
            .Build();
        var repositoryMock = CreateRepositoryMock([user]);
        var unitOfWorkMock = CreateUnitOfWorkMock();
        var handler = new UpdateMyProfileHandler(repositoryMock.Object, unitOfWorkMock.Object);

        var result = await handler.Handle(
            new UpdateMyProfileCommand(
                UserId: user.Id.Value,
                FullName: "Updated Name",
                Email: " Updated.User@Example.com ",
                BirthDate: new DateOnly(1992, 5, 11)),
            CancellationToken.None);

        Assert.Equal(user.Id.Value, result.Id);
        Assert.Equal("Updated Name", result.FullName);
        Assert.Equal("updated.user@example.com", result.Email);
        Assert.Equal(new DateOnly(1992, 5, 11), result.BirthDate);

        unitOfWorkMock.Verify(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        unitOfWorkMock.Verify(x => x.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        unitOfWorkMock.Verify(x => x.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldChangePassword_WhenCurrentPasswordIsValid()
    {
        var user = new UserBuilder()
            .WithPasswordHash("current-hash")
            .WithMustChangePassword(true)
            .Build();
        var repositoryMock = CreateRepositoryMock([user]);
        var passwordHashServiceMock = new Mock<IPasswordHashService>();
        passwordHashServiceMock
            .Setup(x => x.Verify("Current#123", "current-hash"))
            .Returns(true);
        passwordHashServiceMock
            .Setup(x => x.Hash("New#456"))
            .Returns("new-hash");
        var unitOfWorkMock = CreateUnitOfWorkMock();
        var handler = new ChangeMyPasswordHandler(
            repositoryMock.Object,
            passwordHashServiceMock.Object,
            unitOfWorkMock.Object);

        await handler.Handle(
            new ChangeMyPasswordCommand(
                UserId: user.Id.Value,
                CurrentPassword: "Current#123",
                NewPassword: "New#456"),
            CancellationToken.None);

        Assert.Equal("new-hash", user.PasswordHash);
        Assert.False(user.MustChangePassword);
        unitOfWorkMock.Verify(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        unitOfWorkMock.Verify(x => x.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        unitOfWorkMock.Verify(x => x.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    private static Mock<IUserRepository> CreateRepositoryMock(List<User>? initialUsers = null)
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
            .Setup(x => x.Remove(It.IsAny<User>()))
            .Callback((User user) => users.RemoveAll(existing => existing.Id == user.Id));

        return repositoryMock;
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
