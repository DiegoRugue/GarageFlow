using GarageFlow.Application.Vehicles.VehicleBrands.DeleteVehicleBrand;
using Mediator;

namespace GarageFlow.Api.Vehicles.VehicleBrands.DeleteVehicleBrand;

public static class DeleteVehicleBrandEndpoint
{
    public static IEndpointRouteBuilder MapDeleteVehicleBrandEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapDelete("/vehicle-brands/{id:guid}", DeleteVehicleBrand)
            .WithName("DeleteVehicleBrand")
            .WithTags("Vehicle Brands")
            .WithSummary("Delete an existing vehicle brand by ID")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .Produces(StatusCodes.Status500InternalServerError);

        return app;
    }

    private static async Task<IResult> DeleteVehicleBrand(
        Guid id,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var command = new DeleteVehicleBrandCommand(Id: id);
        await mediator.Send(command, cancellationToken);
        return Results.NoContent();
    }
}
