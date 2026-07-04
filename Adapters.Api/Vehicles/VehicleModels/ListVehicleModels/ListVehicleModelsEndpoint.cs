using GarageFlow.Adapters.Api.Vehicles.VehicleModels.GetVehicleModelById;
using GarageFlow.Application.Vehicles.VehicleModels.ListVehicleModels;
using Mediator;

namespace GarageFlow.Adapters.Api.Vehicles.VehicleModels.ListVehicleModels;

public static class ListVehicleModelsEndpoint
{
    public static IEndpointRouteBuilder MapListVehicleModelsEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapGet("/vehicle-models", ListVehicleModels)
            .WithName("ListVehicleModels")
            .WithTags("Vehicle Models")
            .WithSummary("List vehicle models with pagination")
            .Produces<ListVehicleModelsResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        return app;
    }

    private static async Task<IResult> ListVehicleModels(
        IMediator mediator,
        CancellationToken cancellationToken,
        int page = 1,
        int pageSize = 20,
        Guid? vehicleBrandId = null)
    {
        var query = new ListVehicleModelsQuery(page, pageSize, vehicleBrandId);
        var result = await mediator.Send(query, cancellationToken);

        var response = new ListVehicleModelsResponse(
            Items: result.Items
                .Select(dto => new VehicleModelResponse(
                    Id: dto.Id,
                    VehicleBrandId: dto.VehicleBrandId,
                    Name: dto.Name,
                    CreatedAt: dto.CreatedAt))
                .ToList(),
            TotalCount: result.TotalCount,
            Page: result.Page,
            PageSize: result.PageSize);

        return Results.Ok(response);
    }
}
