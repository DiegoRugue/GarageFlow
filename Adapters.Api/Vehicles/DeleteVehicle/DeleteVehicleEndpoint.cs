using GarageFlow.Application.Vehicles.UseCases.DeleteVehicle;
using Mediator;

namespace GarageFlow.Adapters.Api.Vehicles.DeleteVehicle;

public static class DeleteVehicleEndpoint
{
    public static IEndpointRouteBuilder MapDeleteVehicleEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapDelete("/vehicles/{id:guid}", DeleteVehicle)
            .WithName("DeleteVehicle")
            .WithTags("Vehicles")
            .WithSummary("Delete an existing vehicle by ID")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        return app;
    }

    private static async Task<IResult> DeleteVehicle(
        Guid id,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var command = new DeleteVehicleCommand(Id: id);
        await mediator.Send(command, cancellationToken);
        return Results.NoContent();
    }
}
