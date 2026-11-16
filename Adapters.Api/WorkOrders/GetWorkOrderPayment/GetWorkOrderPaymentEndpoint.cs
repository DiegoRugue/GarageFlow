using GarageFlow.Adapters.Api.Security;
using GarageFlow.Application.WorkOrders.UseCases.GetWorkOrderPayment;
using Mediator;

namespace GarageFlow.Adapters.Api.WorkOrders.GetWorkOrderPayment;

public static class GetWorkOrderPaymentEndpoint
{
    public static IEndpointRouteBuilder MapGetWorkOrderPaymentEndpoint(this IEndpointRouteBuilder app)
    {
        AddMetadata(app.MapGet("/work-orders/{id:guid}/payment", GetStaffPayment), "GetWorkOrderPayment");
        return app;
    }

    public static IEndpointRouteBuilder MapGetMyWorkOrderPaymentEndpoint(this IEndpointRouteBuilder app)
    {
        AddMetadata(app.MapGet("/me/work-orders/{id:guid}/payment", GetCustomerPayment), "GetMyWorkOrderPayment");
        return app;
    }

    private static void AddMetadata(RouteHandlerBuilder route, string name)
    {
        route.WithName(name)
            .WithTags("Work Orders")
            .WithSummary("Get the payment status and available checkout for a work order")
            .Produces<GetWorkOrderPaymentResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);
    }

    private static Task<IResult> GetStaffPayment(Guid id, IMediator mediator, CancellationToken cancellationToken) =>
        GetPayment(new GetWorkOrderPaymentQuery(id), mediator, cancellationToken);

    private static Task<IResult> GetCustomerPayment(
        Guid id, HttpContext context, IMediator mediator, CancellationToken cancellationToken) =>
        GetPayment(new GetWorkOrderPaymentQuery(id, context.User.GetRequiredUserId()), mediator, cancellationToken);

    private static async Task<IResult> GetPayment(
        GetWorkOrderPaymentQuery query, IMediator mediator, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(query, cancellationToken);
        return Results.Ok(new GetWorkOrderPaymentResponse(result.ChargeId, result.Status, result.CheckoutUrl, result.ConfirmedAt));
    }
}
