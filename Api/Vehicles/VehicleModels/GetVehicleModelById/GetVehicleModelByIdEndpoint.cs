using GarageFlow.Application.Vehicles.VehicleModels.GetVehicleModelById;
using GarageFlow.BuildingBlocks.Domain.Exceptions;
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
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

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
            throw new NotFoundException($"Vehicle model with ID '{id}' was not found.");
        }

        var response = new VehicleModelResponse(
            Id: result.Id,
            VehicleBrandId: result.VehicleBrandId,
            Name: result.Name,
            CreatedAt: result.CreatedAt);

        return Results.Ok(response);
    }
}
