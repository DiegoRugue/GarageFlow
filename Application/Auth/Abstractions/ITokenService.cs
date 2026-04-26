using GarageFlow.Domain.Users.Entities;

namespace GarageFlow.Application.Auth.Abstractions;

public interface ITokenService
{
    string GenerateToken(User user);
}
