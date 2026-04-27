using GarageFlow.Application.Vehicles.VehicleBrands.GetVehicleBrandById;
using GarageFlow.BuildingBlocks.Domain.Exceptions;
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
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

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
            throw new NotFoundException($"Vehicle brand with ID '{id}' was not found.");
        }

        var response = new VehicleBrandResponse(
            Id: result.Id,
            Name: result.Name,
            CreatedAt: result.CreatedAt);

        return Results.Ok(response);
    }
}
