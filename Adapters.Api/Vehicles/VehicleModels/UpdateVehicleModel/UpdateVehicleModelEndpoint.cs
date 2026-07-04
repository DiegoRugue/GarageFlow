using GarageFlow.Adapters.Api.Vehicles.VehicleModels.GetVehicleModelById;
using GarageFlow.Application.Vehicles.UseCases.VehicleModels.UpdateVehicleModel;
using Mediator;

namespace GarageFlow.Adapters.Api.Vehicles.VehicleModels.UpdateVehicleModel;

public static class UpdateVehicleModelEndpoint
{
    public static IEndpointRouteBuilder MapUpdateVehicleModelEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPut("/vehicle-models/{id:guid}", UpdateVehicleModel)
            .WithName("UpdateVehicleModel")
            .WithTags("Vehicle Models")
            .WithSummary("Update an existing vehicle model")
            .Produces<VehicleModelResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        return app;
    }

    private static async Task<IResult> UpdateVehicleModel(
        Guid id,
        UpdateVehicleModelRequest request,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var command = new UpdateVehicleModelCommand(
            Id: id,
            VehicleBrandId: request.VehicleBrandId,
            Name: request.Name);

        var result = await mediator.Send(command, cancellationToken);

        var response = new VehicleModelResponse(
            Id: result.Id,
            VehicleBrandId: result.VehicleBrandId,
            Name: result.Name,
            CreatedAt: result.CreatedAt);

        return Results.Ok(response);
    }
}
