using GarageFlow.Adapters.Api.Security;
using GarageFlow.Application.Users.ListUsers;
using Mediator;

namespace GarageFlow.Adapters.Api.Users.ListUsers;

public static class ListUsersEndpoint
{
    public static IEndpointRouteBuilder MapListUsersEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapGet("/users", ListUsers)
            .RequireAuthorization(SecurityPolicies.ActiveAdmin)
            .WithName("ListUsers")
            .WithTags("Users")
            .WithSummary("List users with pagination")
            .Produces<ListUsersResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        return app;
    }

    private static async Task<IResult> ListUsers(
        IMediator mediator,
        CancellationToken cancellationToken,
        int page = 1,
        int pageSize = 20)
    {
        var query = new ListUsersQuery(page, pageSize);
        var result = await mediator.Send(query, cancellationToken);

        var response = new ListUsersResponse(
            Items: result.Items
                .Select(user => new UserListItemResponse(
                    Id: user.Id,
                    FullName: user.FullName,
                    Email: user.Email,
                    BirthDate: user.BirthDate,
                    Role: user.Role,
                    MustChangePassword: user.MustChangePassword,
                    CreatedAt: user.CreatedAt,
                    UpdatedAt: user.UpdatedAt))
                .ToList(),
            TotalCount: result.TotalCount,
            Page: result.Page,
            PageSize: result.PageSize);

        return Results.Ok(response);
    }
}
