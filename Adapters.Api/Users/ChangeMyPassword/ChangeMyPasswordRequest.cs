namespace GarageFlow.Adapters.Api.Users.ChangeMyPassword;

public sealed record ChangeMyPasswordRequest(
    string CurrentPassword,
    string NewPassword);
