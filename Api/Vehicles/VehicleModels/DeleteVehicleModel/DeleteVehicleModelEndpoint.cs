using GarageFlow.Application.Vehicles.VehicleModels.DeleteVehicleModel;
using Mediator;

namespace GarageFlow.Api.Vehicles.VehicleModels.DeleteVehicleModel;

public static class DeleteVehicleModelEndpoint
{
    public static IEndpointRouteBuilder MapDeleteVehicleModelEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapDelete("/vehicle-models/{id:guid}", DeleteVehicleModel)
            .WithName("DeleteVehicleModel")
            .WithTags("Vehicle Models")
            .WithSummary("Delete an existing vehicle model by ID")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .Produces(StatusCodes.Status500InternalServerError);

        return app;
    }

    private static async Task<IResult> DeleteVehicleModel(
        Guid id,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var command = new DeleteVehicleModelCommand(Id: id);
        await mediator.Send(command, cancellationToken);
        return Results.NoContent();
    }
}
