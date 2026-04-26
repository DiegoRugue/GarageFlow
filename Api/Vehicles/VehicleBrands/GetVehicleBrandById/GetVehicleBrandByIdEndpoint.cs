using GarageFlow.Application.Vehicles.VehicleBrands.GetVehicleBrandById;
using Mediator;

namespace GarageFlow.Api.Vehicles.VehicleBrands.GetVehicleBrandById;

public static class GetVehicleBrandByIdEndpoint
{
    public static IEndpointRouteBuilder MapGetVehicleBrandByIdEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapGet("/vehicle-brands/{id:guid}", GetVehicleBrandById)
            .WithName("GetVehicleBrandById")
            .WithTags("Vehicle Brands")
            .WithSummary("Get a vehicle brand by its unique identifier")
            .Produces<VehicleBrandResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status500InternalServerError);

        return app;
    }

    private static async Task<IResult> GetVehicleBrandById(
        Guid id,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var query = new GetVehicleBrandByIdQuery(id);
        var result = await mediator.Send(query, cancellationToken);

        if (result is null)
        {
            return Results.Problem(
                title: "Vehicle brand not found",
                detail: $"No vehicle brand found with ID {id}",
                statusCode: StatusCodes.Status404NotFound);
        }

        var response = new VehicleBrandResponse(
            Id: result.Id,
            Name: result.Name,
            CreatedAt: result.CreatedAt);

        return Results.Ok(response);
    }
}
