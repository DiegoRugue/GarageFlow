using GarageFlow.Application.Vehicles.VehicleColors.DeleteVehicleColor;
using Mediator;

namespace GarageFlow.Adapters.Api.Vehicles.VehicleColors.DeleteVehicleColor;

public static class DeleteVehicleColorEndpoint
{
    public static IEndpointRouteBuilder MapDeleteVehicleColorEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapDelete("/vehicle-colors/{id:guid}", DeleteVehicleColor)
            .WithName("DeleteVehicleColor")
            .WithTags("Vehicle Colors")
            .WithSummary("Delete an existing vehicle color by ID")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        return app;
    }

    private static async Task<IResult> DeleteVehicleColor(
        Guid id,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var command = new DeleteVehicleColorCommand(Id: id);
        await mediator.Send(command, cancellationToken);
        return Results.NoContent();
    }
}
