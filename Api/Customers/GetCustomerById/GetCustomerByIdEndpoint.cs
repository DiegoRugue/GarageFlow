using GarageFlow.Application.Customers.GetCustomerById;
using Mediator;

namespace GarageFlow.Api.Customers.GetCustomerById;

public static class GetCustomerByIdEndpoint
{
    public static IEndpointRouteBuilder MapGetCustomerByIdEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapGet("/customers/{id:guid}", GetCustomerById)
            .WithName("GetCustomerById")
            .WithTags("Customers")
            .WithSummary("Get a customer by their unique identifier")
            .Produces<CustomerResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status500InternalServerError);

        return app;
    }

    private static async Task<IResult> GetCustomerById(
        Guid id,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var query = new GetCustomerByIdQuery(id);
        var result = await mediator.Send(query, cancellationToken);

        if (result is null)
        {
            return Results.NotFound(new
            {
                title = "Customer not found",
                detail = $"No customer found with ID {id}",
                status = StatusCodes.Status404NotFound
            });
        }

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
