namespace GarageFlow.Api.Users.UpdateMyProfile;

public sealed record UpdateMyProfileRequest(
    string FullName,
    string Email,
    DateOnly BirthDate);
