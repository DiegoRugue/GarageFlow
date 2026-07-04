using GarageFlow.Application.Vehicles.UseCases.CreateVehicle;
using Mediator;

namespace GarageFlow.Adapters.Api.Vehicles.CreateVehicle;

public static class CreateVehicleEndpoint
{
    public static IEndpointRouteBuilder MapCreateVehicleEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPost("/vehicles", CreateVehicle)
            .WithName("CreateVehicle")
            .WithTags("Vehicles")
            .WithSummary("Create a new vehicle")
            .Produces<CreateVehicleResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        return app;
    }

    private static async Task<IResult> CreateVehicle(
        CreateVehicleRequest request,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var command = new CreateVehicleCommand(
            Plate: request.Plate,
            Year: request.Year,
            CustomerId: request.CustomerId,
            VehicleModelId: request.VehicleModelId,
            VehicleColorId: request.VehicleColorId);

        var result = await mediator.Send(command, cancellationToken);

        var response = new CreateVehicleResponse(
            Id: result.Id,
            CustomerId: result.CustomerId,
            Year: result.Year,
            VehicleBrandId: result.VehicleBrandId,
            VehicleModelId: result.VehicleModelId,
            VehicleColorId: result.VehicleColorId,
            Plate: result.Plate,
            CreatedAt: result.CreatedAt);

        return Results.Created($"/vehicles/{result.Id}", response);
    }
}
