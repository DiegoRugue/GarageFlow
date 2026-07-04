using GarageFlow.Application.WorkOrders.StartDiagnosis;
using Mediator;

namespace GarageFlow.Adapters.Api.WorkOrders.StartDiagnosis;

public static class StartDiagnosisEndpoint
{
    public static IEndpointRouteBuilder MapStartDiagnosisEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPost("/work-orders/{id:guid}/start-diagnosis", StartDiagnosis)
            .WithName("StartDiagnosis")
            .WithTags("Work Orders")
            .WithSummary("Start diagnosis for a work order")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        return app;
    }

    private static async Task<IResult> StartDiagnosis(
        Guid id,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        await mediator.Send(new StartDiagnosisCommand(id), cancellationToken);
        return Results.NoContent();
    }
}
