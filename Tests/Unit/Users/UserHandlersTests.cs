using GarageFlow.Application.Auth.Abstractions;
using GarageFlow.Application.Auth.Login;
using GarageFlow.Application.Users.ChangeMyPassword;
using GarageFlow.Application.Users.CreateUser;
using GarageFlow.Application.Users.UpdateMyProfile;
using GarageFlow.BuildingBlocks.Domain.Exceptions;
using GarageFlow.BuildingBlocks.Domain.ValueObjects;
using GarageFlow.BuildingBlocks.Persistence;
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
    public async Task Handle_ShouldThrowBusinessRuleViolationException_WhenLoginUserDoesNotExist()
    {
        var repositoryMock = CreateRepositoryMock();
        var passwordHashServiceMock = new Mock<IPasswordHashService>();
        var tokenServiceMock = new Mock<ITokenService>();
        var handler = new LoginHandler(
            repositoryMock.Object,
            passwordHashServiceMock.Object,
            tokenServiceMock.Object);

        await Assert.ThrowsAsync<BusinessRuleViolationException>(
            async () => await handler.Handle(
                new LoginCommand("missing.user@example.com", "any-password"),
                CancellationToken.None));

        tokenServiceMock.Verify(x => x.GenerateToken(It.IsAny<User>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldThrowBusinessRuleViolationException_WhenLoginPasswordIsInvalid()
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

        await Assert.ThrowsAsync<BusinessRuleViolationException>(
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
