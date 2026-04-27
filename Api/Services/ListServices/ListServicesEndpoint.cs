using GarageFlow.Api.Services.GetServiceById;
using GarageFlow.Application.Services.ListServices;
using Mediator;

namespace GarageFlow.Api.Services.ListServices;

public static class ListServicesEndpoint
{
    public static IEndpointRouteBuilder MapListServicesEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapGet("/services", ListServices)
            .WithName("ListServices")
            .WithTags("Services")
            .WithSummary("List services with pagination")
            .Produces<ListServicesResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        return app;
    }

    private static async Task<IResult> ListServices(
        IMediator mediator,
        CancellationToken cancellationToken,
        int page = 1,
        int pageSize = 20)
    {
        var query = new ListServicesQuery(page, pageSize);
        var result = await mediator.Send(query, cancellationToken);

        var response = new ListServicesResponse(
            Items: result.Items
                .Select(dto => new ServiceResponse(
                    Id: dto.Id,
                    Description: dto.Description,
                    Price: dto.Price,
                    CreatedAt: dto.CreatedAt))
                .ToList(),
            TotalCount: result.TotalCount,
            Page: result.Page,
            PageSize: result.PageSize);

        return Results.Ok(response);
    }
}
