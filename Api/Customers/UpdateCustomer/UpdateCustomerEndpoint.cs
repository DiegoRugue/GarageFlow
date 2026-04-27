using GarageFlow.Api.Customers.GetCustomerById;
using GarageFlow.Application.Customers.UpdateCustomer;
using Mediator;

namespace GarageFlow.Api.Customers.UpdateCustomer;

public static class UpdateCustomerEndpoint
{
    public static IEndpointRouteBuilder MapUpdateCustomerEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPut("/customers/{id:guid}", UpdateCustomer)
            .WithName("UpdateCustomer")
            .WithTags("Customers")
            .WithSummary("Update an existing customer's mutable fields")
            .Produces<CustomerResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        return app;
    }

    private static async Task<IResult> UpdateCustomer(
        Guid id,
        UpdateCustomerRequest request,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var command = new UpdateCustomerCommand(
            Id: id,
            FullName: request.FullName,
            Email: request.Email,
            PhoneNumber: request.PhoneNumber);

        var result = await mediator.Send(command, cancellationToken);

        var response = new CustomerResponse(
            Id: result.Id,
            TaxDocument: result.TaxDocument,
            TaxDocumentType: result.TaxDocumentType,
            FullName: result.FullName,
            Email: result.Email,
            PhoneNumber: result.PhoneNumber,
            CreatedAt: result.CreatedAt);

        return Results.Ok(response);
    }
}
