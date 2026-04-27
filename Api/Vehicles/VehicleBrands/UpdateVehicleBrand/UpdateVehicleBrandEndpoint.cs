using GarageFlow.Api.Vehicles.VehicleBrands.GetVehicleBrandById;
using GarageFlow.Application.Vehicles.VehicleBrands.UpdateVehicleBrand;
using Mediator;

namespace GarageFlow.Api.Vehicles.VehicleBrands.UpdateVehicleBrand;

public static class UpdateVehicleBrandEndpoint
{
    public static IEndpointRouteBuilder MapUpdateVehicleBrandEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPut("/vehicle-brands/{id:guid}", UpdateVehicleBrand)
            .WithName("UpdateVehicleBrand")
            .WithTags("Vehicle Brands")
            .WithSummary("Update an existing vehicle brand")
            .Produces<VehicleBrandResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        return app;
    }

    private static async Task<IResult> UpdateVehicleBrand(
        Guid id,
        UpdateVehicleBrandRequest request,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var command = new UpdateVehicleBrandCommand(Id: id, Name: request.Name);
        var result = await mediator.Send(command, cancellationToken);

        var response = new VehicleBrandResponse(
            Id: result.Id,
            Name: result.Name,
            CreatedAt: result.CreatedAt);

        return Results.Ok(response);
    }
}
