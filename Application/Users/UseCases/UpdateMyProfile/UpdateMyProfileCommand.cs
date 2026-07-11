using GarageFlow.Application.Common.Messaging;

namespace GarageFlow.Application.Users.UseCases.UpdateMyProfile;

public sealed record UpdateMyProfileCommand(
    Guid UserId,
    string FullName,
    string Email,
    DateOnly BirthDate) : ICommand<UpdateMyProfileResult>;
