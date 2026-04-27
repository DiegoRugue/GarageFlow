using GarageFlow.Api.Services.GetServiceById;
using GarageFlow.Application.Services.UpdateService;
using Mediator;

namespace GarageFlow.Api.Services.UpdateService;

public static class UpdateServiceEndpoint
{
    public static IEndpointRouteBuilder MapUpdateServiceEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPut("/services/{id:guid}", UpdateService)
            .WithName("UpdateService")
            .WithTags("Services")
            .WithSummary("Update an existing service")
            .Produces<ServiceResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        return app;
    }

    private static async Task<IResult> UpdateService(
        Guid id,
        UpdateServiceRequest request,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var command = new UpdateServiceCommand(
            Id: id,
            Description: request.Description,
            Price: request.Price);

        var result = await mediator.Send(command, cancellationToken);

        var response = new ServiceResponse(
            Id: result.Id,
            Description: result.Description,
            Price: result.Price,
            CreatedAt: result.CreatedAt);

        return Results.Ok(response);
    }
}
