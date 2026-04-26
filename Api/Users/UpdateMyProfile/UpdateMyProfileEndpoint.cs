using GarageFlow.Api.Security;
using GarageFlow.Application.Users.UpdateMyProfile;
using Mediator;

namespace GarageFlow.Api.Users.UpdateMyProfile;

public static class UpdateMyProfileEndpoint
{
    public static IEndpointRouteBuilder MapUpdateMyProfileEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPut("/users/me", UpdateMyProfile)
            .RequireAuthorization()
            .WithName("UpdateMyProfile")
            .WithTags("Users")
            .WithSummary("Update authenticated user profile")
            .Produces<UpdateMyProfileResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesValidationProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .Produces(StatusCodes.Status500InternalServerError);

        return app;
    }

    private static async Task<IResult> UpdateMyProfile(
        HttpContext httpContext,
        UpdateMyProfileRequest request,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var userId = httpContext.User.GetRequiredUserId();
        var command = new UpdateMyProfileCommand(
            UserId: userId,
            FullName: request.FullName,
            Email: request.Email,
            BirthDate: request.BirthDate);

        var result = await mediator.Send(command, cancellationToken);
        var response = new UpdateMyProfileResponse(
            Id: result.Id,
            FullName: result.FullName,
            Email: result.Email,
            BirthDate: result.BirthDate,
            Role: result.Role,
            MustChangePassword: result.MustChangePassword,
            CreatedAt: result.CreatedAt,
            UpdatedAt: result.UpdatedAt);

        return Results.Ok(response);
    }
}
