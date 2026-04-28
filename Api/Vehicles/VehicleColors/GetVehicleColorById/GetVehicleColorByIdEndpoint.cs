using GarageFlow.Application.Vehicles.VehicleColors.GetVehicleColorById;
using Mediator;

namespace GarageFlow.Api.Vehicles.VehicleColors.GetVehicleColorById;

public static class GetVehicleColorByIdEndpoint
{
    public static IEndpointRouteBuilder MapGetVehicleColorByIdEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapGet("/vehicle-colors/{id:guid}", GetVehicleColorById)
            .WithName("GetVehicleColorById")
            .WithTags("Vehicle Colors")
            .WithSummary("Get a vehicle color by its unique identifier")
            .Produces<VehicleColorResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        return app;
    }

    private static async Task<IResult> GetVehicleColorById(
        Guid id,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var query = new GetVehicleColorByIdQuery(id);
        var result = await mediator.Send(query, cancellationToken);

        var response = new VehicleColorResponse(
            Id: result.Id,
            Name: result.Name,
            CreatedAt: result.CreatedAt);

        return Results.Ok(response);
    }
}
