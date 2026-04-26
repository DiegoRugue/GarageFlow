using GarageFlow.Api.Vehicles.GetVehicleById;
using GarageFlow.Application.Vehicles.UpdateVehicle;
using Mediator;

namespace GarageFlow.Api.Vehicles.UpdateVehicle;

public static class UpdateVehicleEndpoint
{
    public static IEndpointRouteBuilder MapUpdateVehicleEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPut("/vehicles/{id:guid}", UpdateVehicle)
            .WithName("UpdateVehicle")
            .WithTags("Vehicles")
            .WithSummary("Update an existing vehicle")
            .Produces<VehicleResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesValidationProblem(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status500InternalServerError);

        return app;
    }

    private static async Task<IResult> UpdateVehicle(
        Guid id,
        UpdateVehicleRequest request,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var command = new UpdateVehicleCommand(
            Id: id,
            Plate: request.Plate,
            Year: request.Year,
            CustomerId: request.CustomerId,
            VehicleModelId: request.VehicleModelId,
            VehicleColorId: request.VehicleColorId);

        var result = await mediator.Send(command, cancellationToken);

        var response = new VehicleResponse(
            Id: result.Id,
            CustomerId: result.CustomerId,
            Year: result.Year,
            VehicleBrandId: result.VehicleBrandId,
            VehicleModelId: result.VehicleModelId,
            VehicleColorId: result.VehicleColorId,
            Plate: result.Plate,
            CreatedAt: result.CreatedAt);

        return Results.Ok(response);
    }
}
