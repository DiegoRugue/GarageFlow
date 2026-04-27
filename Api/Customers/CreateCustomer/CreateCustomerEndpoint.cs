using GarageFlow.Application.Customers.CreateCustomer;
using Mediator;

namespace GarageFlow.Api.Customers.CreateCustomer;

public static class CreateCustomerEndpoint
{
    public static IEndpointRouteBuilder MapCreateCustomerEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPost("/customers", CreateCustomer)
            .WithName("CreateCustomer")
            .WithTags("Customers")
            .WithSummary("Create a new customer")
            .Produces<CreateCustomerResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        return app;
    }

    private static async Task<IResult> CreateCustomer(
        CreateCustomerRequest request,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var command = new CreateCustomerCommand(
            TaxDocument: request.TaxDocument,
            FullName: request.FullName,
            Email: request.Email,
            PhoneNumber: request.PhoneNumber);

        var result = await mediator.Send(command, cancellationToken);

        var response = new CreateCustomerResponse(
            Id: result.Id,
            TaxDocument: result.TaxDocument,
            TaxDocumentType: result.TaxDocumentType,
            FullName: result.FullName,
            Email: result.Email,
            PhoneNumber: result.PhoneNumber,
            CreatedAt: result.CreatedAt);

        return Results.Created($"/customers/{result.Id}", response);
    }
}
