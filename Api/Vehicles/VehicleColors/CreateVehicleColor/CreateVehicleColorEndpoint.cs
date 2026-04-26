using GarageFlow.Application.Vehicles.VehicleColors.CreateVehicleColor;
using Mediator;

namespace GarageFlow.Api.Vehicles.VehicleColors.CreateVehicleColor;

public static class CreateVehicleColorEndpoint
{
    public static IEndpointRouteBuilder MapCreateVehicleColorEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPost("/vehicle-colors", CreateVehicleColor)
            .WithName("CreateVehicleColor")
            .WithTags("Vehicle Colors")
            .WithSummary("Create a new vehicle color")
            .Produces<CreateVehicleColorResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .Produces(StatusCodes.Status500InternalServerError);

        return app;
    }

    private static async Task<IResult> CreateVehicleColor(
        CreateVehicleColorRequest request,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var command = new CreateVehicleColorCommand(request.Name);
        var result = await mediator.Send(command, cancellationToken);

        var response = new CreateVehicleColorResponse(
            Id: result.Id,
            Name: result.Name,
            CreatedAt: result.CreatedAt);

        return Results.Created($"/vehicle-colors/{result.Id}", response);
    }
}
