using GarageFlow.Application.Vehicles.VehicleBrands.CreateVehicleBrand;
using Mediator;

namespace GarageFlow.Adapters.Api.Vehicles.VehicleBrands.CreateVehicleBrand;

public static class CreateVehicleBrandEndpoint
{
    public static IEndpointRouteBuilder MapCreateVehicleBrandEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPost("/vehicle-brands", CreateVehicleBrand)
            .WithName("CreateVehicleBrand")
            .WithTags("Vehicle Brands")
            .WithSummary("Create a new vehicle brand")
            .Produces<CreateVehicleBrandResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        return app;
    }

    private static async Task<IResult> CreateVehicleBrand(
        CreateVehicleBrandRequest request,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var command = new CreateVehicleBrandCommand(request.Name);
        var result = await mediator.Send(command, cancellationToken);

        var response = new CreateVehicleBrandResponse(
            Id: result.Id,
            Name: result.Name,
            CreatedAt: result.CreatedAt);

        return Results.Created($"/vehicle-brands/{result.Id}", response);
    }
}
