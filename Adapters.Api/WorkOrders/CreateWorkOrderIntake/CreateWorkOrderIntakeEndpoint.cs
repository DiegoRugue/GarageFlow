using GarageFlow.Application.WorkOrders.UseCases.CreateWorkOrderIntake;
using Mediator;

namespace GarageFlow.Adapters.Api.WorkOrders.CreateWorkOrderIntake;

public static class CreateWorkOrderIntakeEndpoint
{
    public static IEndpointRouteBuilder MapCreateWorkOrderIntakeEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPost("/work-orders/intake", CreateWorkOrderIntake)
            .WithName("CreateWorkOrderIntake")
            .WithTags("Work Orders")
            .WithSummary("Create a complete work-order intake")
            .Produces<CreateWorkOrderIntakeResponse>(StatusCodes.Status201Created)
            .Produces<CreateWorkOrderIntakeResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        return app;
    }

    private static async Task<IResult> CreateWorkOrderIntake(
        CreateWorkOrderIntakeRequest request,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var command = new CreateWorkOrderIntakeCommand(
            request.RequestId,
            request.Customer is null
                ? null
                : new IntakeCustomerInput(
                    request.Customer.TaxDocument,
                    request.Customer.FullName,
                    request.Customer.Email,
                    request.Customer.PhoneNumber),
            request.Vehicle is null
                ? null
                : new IntakeVehicleInput(
                    request.Vehicle.Plate,
                    request.Vehicle.Year,
                    request.Vehicle.Brand,
                    request.Vehicle.Model,
                    request.Vehicle.Color),
            request.Services?.Select(service => service is null
                ? null
                : new IntakeServiceInput(service.Description, service.Price)).ToList(),
            request.InventoryItems?.Select(item => item is null
                ? null
                : new IntakeInventoryItemInput(
                    item.Name,
                    item.Description,
                    item.Type,
                    item.Cost,
                    item.Price,
                    item.StockQuantity,
                    item.Quantity)).ToList());
        var result = await mediator.Send(command, cancellationToken);

        var response = new CreateWorkOrderIntakeResponse(
            result.WorkOrderId,
            result.CustomerId,
            result.VehicleId,
            result.EstimateId,
            result.ServiceIds,
            result.InventoryItemIds,
            result.Status,
            result.CreatedAt);

        return result.IsReplay
            ? Results.Ok(response)
            : Results.Created($"/work-orders/{result.WorkOrderId}", response);
    }
}
