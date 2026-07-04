using GarageFlow.Application.Vehicles.VehicleModels.CreateVehicleModel;
using Mediator;

namespace GarageFlow.Adapters.Api.Vehicles.VehicleModels.CreateVehicleModel;

public static class CreateVehicleModelEndpoint
{
    public static IEndpointRouteBuilder MapCreateVehicleModelEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPost("/vehicle-models", CreateVehicleModel)
            .WithName("CreateVehicleModel")
            .WithTags("Vehicle Models")
            .WithSummary("Create a new vehicle model")
            .Produces<CreateVehicleModelResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        return app;
    }

    private static async Task<IResult> CreateVehicleModel(
        CreateVehicleModelRequest request,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var command = new CreateVehicleModelCommand(
            VehicleBrandId: request.VehicleBrandId,
            Name: request.Name);

        var result = await mediator.Send(command, cancellationToken);

        var response = new CreateVehicleModelResponse(
            Id: result.Id,
            VehicleBrandId: result.VehicleBrandId,
            Name: result.Name,
            CreatedAt: result.CreatedAt);

        return Results.Created($"/vehicle-models/{result.Id}", response);
    }
}
