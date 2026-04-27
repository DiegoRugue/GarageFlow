using GarageFlow.Application.Services.GetServiceById;
using GarageFlow.BuildingBlocks.Domain.Exceptions;
using Mediator;

namespace GarageFlow.Api.Services.GetServiceById;

public static class GetServiceByIdEndpoint
{
    public static IEndpointRouteBuilder MapGetServiceByIdEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapGet("/services/{id:guid}", GetServiceById)
            .WithName("GetServiceById")
            .WithTags("Services")
            .WithSummary("Get a service by its unique identifier")
            .Produces<ServiceResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        return app;
    }

    private static async Task<IResult> GetServiceById(
        Guid id,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var query = new GetServiceByIdQuery(id);
        var result = await mediator.Send(query, cancellationToken);

        if (result is null)
        {
            throw new NotFoundException($"Service with ID '{id}' was not found.");
        }

        var response = new ServiceResponse(
            Id: result.Id,
            Description: result.Description,
            Price: result.Price,
            CreatedAt: result.CreatedAt);

        return Results.Ok(response);
    }
}
