using GarageFlow.BuildingBlocks.Domain.Exceptions;
using GarageFlow.BuildingBlocks.Domain.ValueObjects;
using GarageFlow.Domain.Users.Entities;
using GarageFlow.Domain.Users.Enums;
using GarageFlow.Domain.Users.Events;
using GarageFlow.Domain.Users.ValueObjects;
using GarageFlow.Tests.Shared.Users;

namespace GarageFlow.Tests.Unit.Users;

public class UserDomainTests
{
    [Fact]
    public void GenerateInitialPassword_ShouldUseLowercaseLastNameAndBirthYear()
    {
        var fullName = FullName.Create("Ada Lovelace");
        var birthDate = new DateOnly(1990, 1, 1);

        var initialPassword = User.GenerateInitialPassword(fullName, birthDate);

        Assert.Equal("lovelace1990", initialPassword);
    }

    [Fact]
    public void GenerateInitialPassword_WithDefaultUserBirthDate_ShouldThrowValidationException()
    {
        var fullName = FullName.Create("Ada Lovelace");

        var exception = Assert.Throws<ValidationException>(
            () => User.GenerateInitialPassword(fullName, default(UserBirthDate)));

        Assert.Equal("Birth date cannot be empty.", exception.Message);
    }

    [Fact]
    public void UserBirthDate_Create_WithPastDate_ShouldSucceed()
    {
        var birthDate = UserBirthDate.Create(new DateOnly(1990, 1, 1));

        Assert.Equal(new DateOnly(1990, 1, 1), birthDate.Value);
    }

    [Fact]
    public void UserBirthDate_Create_WithFutureDate_ShouldThrowValidationException()
    {
        var farFutureBirthDate = new DateOnly(2999, 12, 31);

        var exception = Assert.Throws<ValidationException>(() => UserBirthDate.Create(farFutureBirthDate));

        Assert.Equal("Birth date cannot be in the future.", exception.Message);
    }

    [Fact]
    public void Create_ShouldThrowValidationException_WhenRoleIsInvalid()
    {
        var exception = Assert.Throws<ValidationException>(() => User.Create(
            fullName: FullName.Create("User Invalid Role"),
            email: Email.Create("invalid.role@example.com"),
            birthDate: new DateOnly(1990, 2, 3),
            role: (UserRole)999,
            passwordHash: "hash-password"));

        Assert.Equal("User role '999' is invalid.", exception.Message);
    }

    [Fact]
    public void Create_ShouldThrowValidationException_WhenBirthDateIsInFuture()
    {
        var farFutureBirthDate = new DateOnly(2999, 12, 31);

        var exception = Assert.Throws<ValidationException>(() => User.Create(
            fullName: FullName.Create("Future User"),
            email: Email.Create("future.user@example.com"),
            birthDate: farFutureBirthDate,
            role: UserRole.Attendant,
            passwordHash: "hash-password"));

        Assert.Equal("Birth date cannot be in the future.", exception.Message);
    }

    [Fact]
    public void ChangePassword_ShouldDisableMustChangePassword()
    {
        var user = new UserBuilder()
            .WithPasswordHash("hash-initial")
            .WithMustChangePassword(true)
            .Build();

        user.ChangePassword("hash-updated");

        Assert.False(user.MustChangePassword);
        Assert.Equal("hash-updated", user.PasswordHash);
    }

    [Fact]
    public void Create_ShouldRaiseUserCreatedDomainEvent()
    {
        var user = User.Create(
            fullName: FullName.Create("User Event"),
            email: Email.Create("user.event@example.com"),
            birthDate: new DateOnly(1990, 2, 3),
            role: UserRole.Attendant,
            passwordHash: "hash-password");

        var domainEvent = Assert.Single(user.DomainEvents);
        var userCreated = Assert.IsType<UserCreated>(domainEvent);

        Assert.Equal(user.Id, userCreated.UserId);
        Assert.Equal(user.Email.Value, userCreated.Email);
    }

    [Fact]
    public void UpdateProfile_ShouldRaiseUserProfileUpdatedDomainEvent()
    {
        var user = new UserBuilder().Build();
        user.ClearDomainEvents();

        user.UpdateProfile(
            FullName.Create("Updated User"),
            Email.Create("updated.user@example.com"),
            new DateOnly(1991, 1, 2));

        var domainEvent = Assert.Single(user.DomainEvents);
        var profileUpdated = Assert.IsType<UserProfileUpdated>(domainEvent);

        Assert.Equal(user.Id, profileUpdated.UserId);
        Assert.Equal("updated.user@example.com", profileUpdated.Email);
    }

    [Fact]
    public void ChangePassword_ShouldRaiseUserPasswordChangedDomainEvent()
    {
        var user = new UserBuilder()
            .WithPasswordHash("hash-initial")
            .WithMustChangePassword(true)
            .Build();
        user.ClearDomainEvents();

        user.ChangePassword("hash-updated");

        var domainEvent = Assert.Single(user.DomainEvents);
        var passwordChanged = Assert.IsType<UserPasswordChanged>(domainEvent);

        Assert.Equal(user.Id, passwordChanged.UserId);
        Assert.False(passwordChanged.MustChangePassword);
    }
}
