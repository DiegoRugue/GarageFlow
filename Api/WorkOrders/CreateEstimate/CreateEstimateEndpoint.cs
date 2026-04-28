using GarageFlow.Application.WorkOrders.CreateEstimate;
using Mediator;

namespace GarageFlow.Api.WorkOrders.CreateEstimate;

public static class CreateEstimateEndpoint
{
    public static IEndpointRouteBuilder MapCreateEstimateEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPost("/work-orders/{id:guid}/estimates", CreateEstimate)
            .WithName("CreateEstimate")
            .WithTags("Work Orders")
            .WithSummary("Create a draft estimate for a work order")
            .Produces<CreateEstimateResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        return app;
    }

    private static async Task<IResult> CreateEstimate(
        Guid id,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var command = new CreateEstimateCommand(id);
        var result = await mediator.Send(command, cancellationToken);

        var response = new CreateEstimateResponse(
            Id: result.Id,
            WorkOrderId: result.WorkOrderId,
            Status: result.Status,
            TotalAmount: result.TotalAmount,
            CreatedAt: result.CreatedAt);

        return Results.Created($"/work-orders/{result.WorkOrderId}/estimates/{result.Id}", response);
    }
}
