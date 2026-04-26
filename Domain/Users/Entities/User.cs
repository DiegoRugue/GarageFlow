using GarageFlow.BuildingBlocks.Domain.Entities;
using GarageFlow.BuildingBlocks.Domain.Exceptions;
using GarageFlow.BuildingBlocks.Domain.Interfaces;
using GarageFlow.BuildingBlocks.Domain.ValueObjects;
using GarageFlow.Domain.Users.Enums;
using GarageFlow.Domain.Users.ValueObjects;

namespace GarageFlow.Domain.Users.Entities;

public sealed class User : Entity<UserId>, IAggregateRoot
{
    public FullName FullName { get; private set; }
    public Email Email { get; private set; }
    public DateOnly BirthDate { get; private set; }
    public UserRole Role { get; private set; }
    public string PasswordHash { get; private set; }
    public bool MustChangePassword { get; private set; }

    private User(UserId id) : base(id)
    {
        FullName = null!;
        Email = null!;
        PasswordHash = null!;
    }

    private User(
        UserId id,
        FullName fullName,
        Email email,
        DateOnly birthDate,
        UserRole role,
        string passwordHash,
        bool mustChangePassword) : base(id)
    {
        FullName = fullName;
        Email = NormalizeEmail(email);
        BirthDate = EnsureValidBirthDate(birthDate);
        Role = EnsureValidRole(role);
        PasswordHash = EnsurePasswordHash(passwordHash);
        MustChangePassword = mustChangePassword;
    }

    public static User Create(
        FullName fullName,
        Email email,
        DateOnly birthDate,
        UserRole role,
        string passwordHash)
    {
        return new User(
            id: UserId.New(),
            fullName: fullName,
            email: email,
            birthDate: birthDate,
            role: role,
            passwordHash: passwordHash,
            mustChangePassword: true);
    }

    public void UpdateProfile(FullName fullName, Email email, DateOnly birthDate)
    {
        FullName = fullName;
        Email = NormalizeEmail(email);
        BirthDate = EnsureValidBirthDate(birthDate);
        UpdatedAt = DateTime.UtcNow;
    }

    public void ChangePassword(string newPasswordHash)
    {
        PasswordHash = EnsurePasswordHash(newPasswordHash);
        MustChangePassword = false;
        UpdatedAt = DateTime.UtcNow;
    }

    public static string GenerateInitialPassword(FullName fullName, DateOnly birthDate)
    {
        var nameParts = fullName.Value.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (nameParts.Length == 0)
        {
            throw new ValidationException("Full name must contain at least one valid name part.");
        }

        var lastName = nameParts[^1].Trim().ToLowerInvariant();
        return $"{lastName}{birthDate.Year}";
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

    private static DateOnly EnsureValidBirthDate(DateOnly birthDate)
    {
        if (birthDate == default || birthDate == DateOnly.MinValue)
        {
            throw new ValidationException("Birth date cannot be empty.");
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        if (birthDate > today)
        {
            throw new ValidationException("Birth date cannot be in the future.");
        }

        return birthDate;
    }
}
