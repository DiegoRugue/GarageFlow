using GarageFlow.Application.Vehicles.VehicleModels.GetVehicleModelById;
using Mediator;

namespace GarageFlow.Api.Vehicles.VehicleModels.GetVehicleModelById;

public static class GetVehicleModelByIdEndpoint
{
    public static IEndpointRouteBuilder MapGetVehicleModelByIdEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapGet("/vehicle-models/{id:guid}", GetVehicleModelById)
            .WithName("GetVehicleModelById")
            .WithTags("Vehicle Models")
            .WithSummary("Get a vehicle model by its unique identifier")
            .Produces<VehicleModelResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status500InternalServerError);

        return app;
    }

    private static async Task<IResult> GetVehicleModelById(
        Guid id,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var query = new GetVehicleModelByIdQuery(id);
        var result = await mediator.Send(query, cancellationToken);

        if (result is null)
        {
            return Results.Problem(
                title: "Vehicle model not found",
                detail: $"No vehicle model found with ID {id}",
                statusCode: StatusCodes.Status404NotFound);
        }

        var response = new VehicleModelResponse(
            Id: result.Id,
            VehicleBrandId: result.VehicleBrandId,
            Name: result.Name,
            CreatedAt: result.CreatedAt);

        return Results.Ok(response);
    }
}
