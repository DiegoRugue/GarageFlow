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
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        return app;
    }

    private static async Task<IResult> GetVehicleById(
        Guid id,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var query = new GetVehicleByIdQuery(id);
        var result = await mediator.Send(query, cancellationToken);

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
