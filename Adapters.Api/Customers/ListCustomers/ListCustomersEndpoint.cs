using GarageFlow.Adapters.Api.Customers.GetCustomerById;
using GarageFlow.Application.Customers.UseCases.ListCustomers;
using Mediator;

namespace GarageFlow.Adapters.Api.Customers.ListCustomers;

public static class ListCustomersEndpoint
{
    public static IEndpointRouteBuilder MapListCustomersEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapGet("/customers", ListCustomers)
            .WithName("ListCustomers")
            .WithTags("Customers")
            .WithSummary("List customers with pagination")
            .Produces<ListCustomersResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        return app;
    }

    private static async Task<IResult> ListCustomers(
        IMediator mediator,
        CancellationToken cancellationToken,
        int page = 1,
        int pageSize = 20)
    {
        var query = new ListCustomersQuery(page, pageSize);
        var result = await mediator.Send(query, cancellationToken);

        var response = new ListCustomersResponse(
            Items: result.Items
                .Select(dto => new CustomerResponse(
                    Id: dto.Id,
                    TaxDocument: dto.TaxDocument,
                    TaxDocumentType: dto.TaxDocumentType,
                    FullName: dto.FullName,
                    Email: dto.Email,
                    PhoneNumber: dto.PhoneNumber,
                    Status: dto.Status,
                    CreatedAt: dto.CreatedAt))
                .ToList(),
            TotalCount: result.TotalCount,
            Page: result.Page,
            PageSize: result.PageSize);

        return Results.Ok(response);
    }
}
