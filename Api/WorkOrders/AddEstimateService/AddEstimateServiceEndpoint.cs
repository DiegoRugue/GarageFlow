using GarageFlow.Application.WorkOrders.AddEstimateService;
using Mediator;

namespace GarageFlow.Api.WorkOrders.AddEstimateService;

public static class AddEstimateServiceEndpoint
{
    public static IEndpointRouteBuilder MapAddEstimateServiceEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPost("/work-orders/{id:guid}/estimates/{estimateId:guid}/services", AddEstimateService)
            .WithName("AddEstimateService")
            .WithTags("Work Orders")
            .WithSummary("Add a service line to a work order estimate")
            .Produces<AddEstimateServiceResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        return app;
    }

    private static async Task<IResult> AddEstimateService(
        Guid id,
        Guid estimateId,
        AddEstimateServiceRequest request,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var command = new AddEstimateServiceCommand(id, estimateId, request.ServiceId);
        var result = await mediator.Send(command, cancellationToken);
        var response = new AddEstimateServiceResponse(
            EstimateId: result.EstimateId,
            ServiceId: result.ServiceId,
            Description: result.Description,
            UnitPrice: result.UnitPrice,
            TotalPrice: result.TotalPrice);

        return Results.Ok(response);
    }
}
