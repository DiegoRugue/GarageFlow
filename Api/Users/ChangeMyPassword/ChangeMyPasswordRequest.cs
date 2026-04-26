namespace GarageFlow.Api.Users.ChangeMyPassword;

public sealed record ChangeMyPasswordRequest(
    string CurrentPassword,
    string NewPassword);
