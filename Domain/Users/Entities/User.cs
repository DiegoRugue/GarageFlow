using GarageFlow.SharedKernel.Domain.Entities;
using GarageFlow.SharedKernel.Domain.Exceptions;
using GarageFlow.SharedKernel.Domain.Interfaces;
using GarageFlow.SharedKernel.Domain.ValueObjects;
using GarageFlow.Domain.Customers.ValueObjects;
using GarageFlow.Domain.Users.Enums;
using GarageFlow.Domain.Users.Events;
using GarageFlow.Domain.Users.ValueObjects;

namespace GarageFlow.Domain.Users.Entities;

public sealed class User : Entity<UserId>, IAggregateRoot
{
    public FullName FullName { get; private set; }
    public Email Email { get; private set; }
    public UserBirthDate BirthDate { get; private set; }
    public UserRole Role { get; private set; }
    public CustomerId? CustomerId { get; private set; }
    public string PasswordHash { get; private set; }
    public bool MustChangePassword { get; private set; }

    private User(
        UserId id,
        FullName fullName,
        Email email,
        UserBirthDate birthDate,
        UserRole role,
        string passwordHash,
        bool mustChangePassword,
        CustomerId? customerId = null) : base(id)
    {
        FullName = fullName;
        Email = NormalizeEmail(email);
        BirthDate = EnsureValidBirthDate(birthDate);
        Role = EnsureValidRole(role);
        CustomerId = EnsureValidCustomerLink(role, customerId);
        PasswordHash = EnsurePasswordHash(passwordHash);
        MustChangePassword = mustChangePassword;
    }

    public static User Create(
        FullName fullName,
        Email email,
        DateOnly birthDate,
        UserRole role,
        string passwordHash,
        CustomerId? customerId = null)
    {
        return Create(
            fullName,
            email,
            UserBirthDate.Create(birthDate),
            role,
            passwordHash,
            customerId);
    }

    public static User Create(
        FullName fullName,
        Email email,
        UserBirthDate birthDate,
        UserRole role,
        string passwordHash,
        CustomerId? customerId = null)
    {
        var user = new User(
            id: UserId.New(),
            fullName: fullName,
            email: email,
            birthDate: birthDate,
            role: role,
            passwordHash: passwordHash,
            mustChangePassword: true,
            customerId: customerId);

        user.RaiseDomainEvent(new UserCreated(
            UserId: user.Id,
            FullName: user.FullName.Value,
            Email: user.Email.Value,
            BirthDate: user.BirthDate.Value,
            Role: user.Role,
            CustomerId: user.CustomerId,
            MustChangePassword: user.MustChangePassword,
            CreatedAt: user.CreatedAt));

        return user;
    }

    public void UpdateProfile(FullName fullName, Email email, DateOnly birthDate)
    {
        UpdateProfile(fullName, email, UserBirthDate.Create(birthDate));
    }

    public void UpdateProfile(FullName fullName, Email email, UserBirthDate birthDate)
    {
        FullName = fullName;
        Email = NormalizeEmail(email);
        BirthDate = EnsureValidBirthDate(birthDate);
        UpdatedAt = DateTime.UtcNow;

        RaiseDomainEvent(new UserProfileUpdated(
            UserId: Id,
            FullName: FullName.Value,
            Email: Email.Value,
            BirthDate: BirthDate.Value,
            Role: Role,
            MustChangePassword: MustChangePassword,
            UpdatedAt: UpdatedAt));
    }

    public void ChangePassword(string newPasswordHash)
    {
        PasswordHash = EnsurePasswordHash(newPasswordHash);
        MustChangePassword = false;
        UpdatedAt = DateTime.UtcNow;

        RaiseDomainEvent(new UserPasswordChanged(
            UserId: Id,
            MustChangePassword: MustChangePassword,
            UpdatedAt: UpdatedAt));
    }

    public static string GenerateInitialPassword(FullName fullName, DateOnly birthDate)
    {
        return GenerateInitialPassword(fullName, UserBirthDate.Create(birthDate));
    }

    public static string GenerateInitialPassword(FullName fullName, UserBirthDate birthDate)
    {
        var validatedBirthDate = EnsureValidBirthDate(birthDate);
        var nameParts = fullName.Value.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (nameParts.Length == 0)
        {
            throw new ValidationException("Full name must contain at least one valid name part.");
        }

        var lastName = nameParts[^1].Trim().ToLowerInvariant();
        return $"{lastName}{validatedBirthDate.Value.Year}";
    }

    private static Email NormalizeEmail(Email email)
    {
        return Email.Create(email.Value.ToLowerInvariant());
    }

    private static string EnsurePasswordHash(string passwordHash)
    {
        if (string.IsNullOrWhiteSpace(passwordHash))
        {
            throw new ValidationException("Password hash cannot be empty or whitespace.");
        }

        return passwordHash.Trim();
    }

    private static UserRole EnsureValidRole(UserRole role)
    {
        if (!Enum.IsDefined(role))
        {
            throw new ValidationException($"User role '{(int)role}' is invalid.");
        }

        return role;
    }

    private static CustomerId? EnsureValidCustomerLink(UserRole role, CustomerId? customerId)
    {
        if (role == UserRole.Customer && customerId is null)
        {
            throw new ValidationException("Customer users must be linked to a customer.");
        }

        if (role == UserRole.Customer && customerId is CustomerId id && id.Value == Guid.Empty)
        {
            throw new ValidationException("Customer users must be linked to a customer.");
        }

        if (role != UserRole.Customer && customerId is not null)
        {
            throw new ValidationException("Only customer users can be linked to a customer.");
        }

        return customerId;
    }

    private static UserBirthDate EnsureValidBirthDate(UserBirthDate birthDate) => UserBirthDate.Create(birthDate.Value);
}
