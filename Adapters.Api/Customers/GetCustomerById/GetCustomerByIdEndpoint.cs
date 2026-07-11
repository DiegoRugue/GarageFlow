using GarageFlow.Application.Customers.UseCases.GetCustomerById;
using Mediator;

namespace GarageFlow.Adapters.Api.Customers.GetCustomerById;

public static class GetCustomerByIdEndpoint
{
    public static IEndpointRouteBuilder MapGetCustomerByIdEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapGet("/customers/{id:guid}", GetCustomerById)
            .WithName("GetCustomerById")
            .WithTags("Customers")
            .WithSummary("Get a customer by their unique identifier")
            .Produces<CustomerResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        return app;
    }

    private static async Task<IResult> GetCustomerById(
        Guid id,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var query = new GetCustomerByIdQuery(id);
        var result = await mediator.Send(query, cancellationToken);

        var response = new CustomerResponse(
            Id: result.Id,
            TaxDocument: result.TaxDocument,
            TaxDocumentType: result.TaxDocumentType,
            FullName: result.FullName,
            Email: result.Email,
            PhoneNumber: result.PhoneNumber,
            CreatedAt: result.CreatedAt,
            Vehicles: result.Vehicles?
                .Select(vehicle => new CustomerVehicleResponse(
                    Id: vehicle.Id,
                    Year: vehicle.Year,
                    Plate: vehicle.Plate,
                    VehicleBrandId: vehicle.VehicleBrandId,
                    VehicleBrandName: vehicle.VehicleBrandName,
                    VehicleModelId: vehicle.VehicleModelId,
                    VehicleModelName: vehicle.VehicleModelName,
                    VehicleColorId: vehicle.VehicleColorId,
                    VehicleColorName: vehicle.VehicleColorName,
                    CreatedAt: vehicle.CreatedAt))
                .ToList());

        return Results.Ok(response);
    }
}
