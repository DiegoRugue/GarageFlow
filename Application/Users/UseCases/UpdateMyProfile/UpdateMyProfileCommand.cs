using Mediator;

namespace GarageFlow.Application.Users.UseCases.UpdateMyProfile;

public sealed record UpdateMyProfileCommand(
    Guid UserId,
    string FullName,
    string Email,
    DateOnly BirthDate) : IRequest<UpdateMyProfileResult>;
