using GarageFlow.Api.Vehicles.VehicleColors.GetVehicleColorById;
using GarageFlow.Application.Vehicles.VehicleColors.UpdateVehicleColor;
using Mediator;

namespace GarageFlow.Api.Vehicles.VehicleColors.UpdateVehicleColor;

public static class UpdateVehicleColorEndpoint
{
    public static IEndpointRouteBuilder MapUpdateVehicleColorEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPut("/vehicle-colors/{id:guid}", UpdateVehicleColor)
            .WithName("UpdateVehicleColor")
            .WithTags("Vehicle Colors")
            .WithSummary("Update an existing vehicle color")
            .Produces<VehicleColorResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        return app;
    }

    private static async Task<IResult> UpdateVehicleColor(
        Guid id,
        UpdateVehicleColorRequest request,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var command = new UpdateVehicleColorCommand(Id: id, Name: request.Name);
        var result = await mediator.Send(command, cancellationToken);

        var response = new VehicleColorResponse(
            Id: result.Id,
            Name: result.Name,
            CreatedAt: result.CreatedAt);

        return Results.Ok(response);
    }
}
