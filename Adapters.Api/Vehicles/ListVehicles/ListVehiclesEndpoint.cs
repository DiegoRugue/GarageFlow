using GarageFlow.Adapters.Api.Vehicles.GetVehicleById;
using GarageFlow.Application.Vehicles.UseCases.ListVehicles;
using Mediator;

namespace GarageFlow.Adapters.Api.Vehicles.ListVehicles;

public static class ListVehiclesEndpoint
{
    public static IEndpointRouteBuilder MapListVehiclesEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapGet("/vehicles", ListVehicles)
            .WithName("ListVehicles")
            .WithTags("Vehicles")
            .WithSummary("List vehicles with pagination")
            .Produces<ListVehiclesResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        return app;
    }

    private static async Task<IResult> ListVehicles(
        IMediator mediator,
        CancellationToken cancellationToken,
        int page = 1,
        int pageSize = 20,
        Guid? customerId = null)
    {
        var query = new ListVehiclesQuery(page, pageSize, customerId);
        var result = await mediator.Send(query, cancellationToken);

        var response = new ListVehiclesResponse(
            Items: result.Items
                .Select(dto => new VehicleResponse(
                    Id: dto.Id,
                    CustomerId: dto.CustomerId,
                    Year: dto.Year,
                    VehicleBrandId: dto.VehicleBrandId,
                    VehicleBrandName: dto.VehicleBrandName,
                    VehicleModelId: dto.VehicleModelId,
                    VehicleModelName: dto.VehicleModelName,
                    VehicleColorId: dto.VehicleColorId,
                    VehicleColorName: dto.VehicleColorName,
                    Plate: dto.Plate,
                    CreatedAt: dto.CreatedAt))
                .ToList(),
            TotalCount: result.TotalCount,
            Page: result.Page,
            PageSize: result.PageSize);

        return Results.Ok(response);
    }
}
