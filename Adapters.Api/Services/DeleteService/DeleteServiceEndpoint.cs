using GarageFlow.Application.Services.UseCases.DeleteService;
using Mediator;

namespace GarageFlow.Adapters.Api.Services.DeleteService;

public static class DeleteServiceEndpoint
{
    public static IEndpointRouteBuilder MapDeleteServiceEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapDelete("/services/{id:guid}", DeleteService)
            .WithName("DeleteService")
            .WithTags("Services")
            .WithSummary("Delete an existing service by ID")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        return app;
    }

    private static async Task<IResult> DeleteService(
        Guid id,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var command = new DeleteServiceCommand(Id: id);
        await mediator.Send(command, cancellationToken);
        return Results.NoContent();
    }
}
