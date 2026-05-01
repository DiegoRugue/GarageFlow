using GarageFlow.Application.WorkOrders.GetAverageServiceTime;
using Mediator;

namespace GarageFlow.Api.WorkOrders.GetAverageServiceTime;

public static class GetAverageServiceTimeEndpoint
{
    public static IEndpointRouteBuilder MapGetAverageServiceTimeEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapGet("/work-orders/average-service-time", GetAverageServiceTime)
            .WithName("GetAverageServiceTime")
            .WithTags("Work Orders")
            .WithSummary("Get average service time for completed work orders")
            .Produces<GetAverageServiceTimeResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        return app;
    }

    private static async Task<IResult> GetAverageServiceTime(
        DateTimeOffset from,
        DateTimeOffset to,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(
            new GetAverageServiceTimeQuery(from.UtcDateTime, to.UtcDateTime),
            cancellationToken);

        var response = new GetAverageServiceTimeResponse(
            From: result.From,
            To: result.To,
            CompletedWorkOrdersCount: result.CompletedWorkOrdersCount,
            AverageDurationMinutes: result.AverageDurationMinutes);

        return Results.Ok(response);
    }
}
