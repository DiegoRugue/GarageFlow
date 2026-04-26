using GarageFlow.Api.Vehicles.VehicleColors.GetVehicleColorById;
using GarageFlow.Application.Vehicles.VehicleColors.ListVehicleColors;
using Mediator;

namespace GarageFlow.Api.Vehicles.VehicleColors.ListVehicleColors;

public static class ListVehicleColorsEndpoint
{
    public static IEndpointRouteBuilder MapListVehicleColorsEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapGet("/vehicle-colors", ListVehicleColors)
            .WithName("ListVehicleColors")
            .WithTags("Vehicle Colors")
            .WithSummary("List vehicle colors with pagination")
            .Produces<ListVehicleColorsResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status500InternalServerError);

        return app;
    }

    private static async Task<IResult> ListVehicleColors(
        IMediator mediator,
        CancellationToken cancellationToken,
        int page = 1,
        int pageSize = 20)
    {
        var query = new ListVehicleColorsQuery(page, pageSize);
        var result = await mediator.Send(query, cancellationToken);

        var response = new ListVehicleColorsResponse(
            Items: result.Items
                .Select(dto => new VehicleColorResponse(
                    Id: dto.Id,
                    Name: dto.Name,
                    CreatedAt: dto.CreatedAt))
                .ToList(),
            TotalCount: result.TotalCount,
            Page: result.Page,
            PageSize: result.PageSize);

        return Results.Ok(response);
    }
}
