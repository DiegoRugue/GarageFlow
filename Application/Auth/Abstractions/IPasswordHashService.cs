namespace GarageFlow.Application.Auth.Abstractions;

public interface IPasswordHashService
{
    string Hash(string password);

    bool Verify(string password, string passwordHash);
}
