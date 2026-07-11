using GarageFlow.Application.WorkOrders.UseCases.CreateWorkOrder;
using Mediator;

namespace GarageFlow.Adapters.Api.WorkOrders.CreateWorkOrder;

public static class CreateWorkOrderEndpoint
{
    public static IEndpointRouteBuilder MapCreateWorkOrderEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPost("/work-orders", CreateWorkOrder)
            .WithName("CreateWorkOrder")
            .WithTags("Work Orders")
            .WithSummary("Create a new work order")
            .Produces<CreateWorkOrderResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        return app;
    }

    private static async Task<IResult> CreateWorkOrder(
        CreateWorkOrderRequest request,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var command = new CreateWorkOrderCommand(request.CustomerId, request.VehicleId);
        var result = await mediator.Send(command, cancellationToken);

        var response = new CreateWorkOrderResponse(
            Id: result.Id,
            CustomerId: result.CustomerId,
            VehicleId: result.VehicleId,
            Status: result.Status,
            CreatedAt: result.CreatedAt);

        return Results.Created($"/work-orders/{result.Id}", response);
    }
}
