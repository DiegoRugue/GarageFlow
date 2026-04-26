using GarageFlow.Application.Auth.Abstractions;
using Microsoft.AspNetCore.Identity;

namespace GarageFlow.Infrastructure.Auth;

public sealed class PasswordHashService : IPasswordHashService
{
    private readonly PasswordHasher<object> _passwordHasher = new();

    public string Hash(string password)
    {
        if (string.IsNullOrWhiteSpace(password))
        {
            throw new ArgumentException("Password cannot be empty or whitespace.", nameof(password));
        }

        return _passwordHasher.HashPassword(user: null!, password);
    }

    public bool Verify(string password, string passwordHash)
    {
        if (string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(passwordHash))
        {
            return false;
        }

        var verificationResult = _passwordHasher.VerifyHashedPassword(user: null!, passwordHash, password);
        return verificationResult is PasswordVerificationResult.Success or PasswordVerificationResult.SuccessRehashNeeded;
    }
}
