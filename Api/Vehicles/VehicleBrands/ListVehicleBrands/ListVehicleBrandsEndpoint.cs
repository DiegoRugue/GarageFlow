using GarageFlow.Api.Vehicles.VehicleBrands.GetVehicleBrandById;
using GarageFlow.Application.Vehicles.VehicleBrands.ListVehicleBrands;
using Mediator;

namespace GarageFlow.Api.Vehicles.VehicleBrands.ListVehicleBrands;

public static class ListVehicleBrandsEndpoint
{
    public static IEndpointRouteBuilder MapListVehicleBrandsEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapGet("/vehicle-brands", ListVehicleBrands)
            .WithName("ListVehicleBrands")
            .WithTags("Vehicle Brands")
            .WithSummary("List vehicle brands with pagination")
            .Produces<ListVehicleBrandsResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        return app;
    }

    private static async Task<IResult> ListVehicleBrands(
        IMediator mediator,
        CancellationToken cancellationToken,
        int page = 1,
        int pageSize = 20)
    {
        var query = new ListVehicleBrandsQuery(page, pageSize);
        var result = await mediator.Send(query, cancellationToken);

        var response = new ListVehicleBrandsResponse(
            Items: result.Items
                .Select(dto => new VehicleBrandResponse(
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
