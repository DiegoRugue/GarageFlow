using GarageFlow.Application.Vehicles.GetVehicleById;
using Mediator;

namespace GarageFlow.Api.Vehicles.GetVehicleById;

public static class GetVehicleByIdEndpoint
{
    public static IEndpointRouteBuilder MapGetVehicleByIdEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapGet("/vehicles/{id:guid}", GetVehicleById)
            .WithName("GetVehicleById")
            .WithTags("Vehicles")
            .WithSummary("Get a vehicle by its unique identifier")
            .Produces<VehicleResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status500InternalServerError);

        return app;
    }

    private static async Task<IResult> GetVehicleById(
        Guid id,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var query = new GetVehicleByIdQuery(id);
        var result = await mediator.Send(query, cancellationToken);

        if (result is null)
        {
            return Results.Problem(
                title: "Vehicle not found",
                detail: $"No vehicle found with ID {id}",
                statusCode: StatusCodes.Status404NotFound);
        }

        var response = new VehicleResponse(
            Id: result.Id,
            CustomerId: result.CustomerId,
            Year: result.Year,
            VehicleBrandId: result.VehicleBrandId,
            VehicleBrandName: result.VehicleBrandName,
            VehicleModelId: result.VehicleModelId,
            VehicleModelName: result.VehicleModelName,
            VehicleColorId: result.VehicleColorId,
            VehicleColorName: result.VehicleColorName,
            Plate: result.Plate,
            CreatedAt: result.CreatedAt);

        return Results.Ok(response);
    }
}
