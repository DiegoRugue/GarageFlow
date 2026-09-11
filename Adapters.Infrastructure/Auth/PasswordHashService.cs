using GarageFlow.Application.Auth.Abstractions;
using Microsoft.AspNetCore.Identity;

namespace GarageFlow.Adapters.Infrastructure.Auth;

public sealed class PasswordHashService : IPasswordHashService
{
    private static readonly Lazy<string> MissingUserHash = new(() =>
        new PasswordHasher<object>().HashPassword(null!, Guid.NewGuid().ToString("N")));
    private readonly IPasswordHasher<object> _passwordHasher;

    public PasswordHashService() : this(new PasswordHasher<object>())
    {
    }

    public PasswordHashService(IPasswordHasher<object> passwordHasher)
    {
        ArgumentNullException.ThrowIfNull(passwordHasher);
        _passwordHasher = passwordHasher;
    }

    public string Hash(string password)
    {
        if (string.IsNullOrWhiteSpace(password))
        {
            throw new ArgumentException("Password cannot be empty or whitespace.", nameof(password));
        }

        return _passwordHasher.HashPassword(user: null!, password);
    }

    public bool Verify(string password, string? passwordHash)
    {
        if (string.IsNullOrWhiteSpace(password))
        {
            return false;
        }

        var hasStoredHash = !string.IsNullOrWhiteSpace(passwordHash);
        var verificationResult = _passwordHasher.VerifyHashedPassword(
            user: null!, hasStoredHash ? passwordHash! : MissingUserHash.Value, password);
        return hasStoredHash
            && verificationResult is PasswordVerificationResult.Success or PasswordVerificationResult.SuccessRehashNeeded;
    }
}
