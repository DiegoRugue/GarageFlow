using GarageFlow.SharedKernel.Domain.ValueObjects;
using GarageFlow.Domain.Customers.ValueObjects;
using GarageFlow.Domain.Users.Entities;
using GarageFlow.Domain.Users.Enums;

namespace GarageFlow.Tests.Shared.Users;

public sealed class UserBuilder
{
    private string _fullName = "Alex Attendant";
    private string _email = "alex.attendant@example.com";
    private DateOnly _birthDate = new(1992, 7, 15);
    private UserRole _role = UserRole.Attendant;
    private string _passwordHash = Guid.NewGuid().ToString("N");
    private bool _mustChangePassword = true;
    private CustomerId? _customerId;

    public UserBuilder WithFullName(string fullName)
    {
        _fullName = fullName;
        return this;
    }

    public UserBuilder WithEmail(string email)
    {
        _email = email;
        return this;
    }

    public UserBuilder WithBirthDate(DateOnly birthDate)
    {
        _birthDate = birthDate;
        return this;
    }

    public UserBuilder WithRole(UserRole role)
    {
        _role = role;
        return this;
    }

    public UserBuilder WithPasswordHash(string passwordHash)
    {
        _passwordHash = passwordHash;
        return this;
    }

    public UserBuilder WithMustChangePassword(bool mustChangePassword)
    {
        _mustChangePassword = mustChangePassword;
        return this;
    }

    public UserBuilder WithCustomerId(CustomerId customerId)
    {
        _customerId = customerId;
        return this;
    }

    public User Build()
    {
        var user = User.Create(
            fullName: FullName.Create(_fullName),
            email: Email.Create(_email),
            birthDate: _birthDate,
            role: _role,
            passwordHash: _passwordHash,
            customerId: _customerId);

        if (!_mustChangePassword)
        {
            user.ChangePassword(_passwordHash);
        }

        return user;
    }

    public CreateUserRequest BuildCreateRequest()
    {
        return new CreateUserRequest(
            FullName: _fullName,
            Email: _email,
            BirthDate: _birthDate,
            Role: _role);
    }

    public UpdateMyProfileRequest BuildUpdateMyProfileRequest()
    {
        return new UpdateMyProfileRequest(
            FullName: _fullName,
            Email: _email,
            BirthDate: _birthDate);
    }
}

public sealed record CreateUserRequest(
    string FullName,
    string Email,
    DateOnly BirthDate,
    UserRole Role);

public sealed record UpdateMyProfileRequest(
    string FullName,
    string Email,
    DateOnly BirthDate);

public sealed record ChangeMyPasswordRequest(
    string CurrentPassword,
    string NewPassword);
