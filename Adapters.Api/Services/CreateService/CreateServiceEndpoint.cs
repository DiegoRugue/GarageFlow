using GarageFlow.Application.Services.CreateService;
using Mediator;

namespace GarageFlow.Adapters.Api.Services.CreateService;

public static class CreateServiceEndpoint
{
    public static IEndpointRouteBuilder MapCreateServiceEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPost("/services", CreateService)
            .WithName("CreateService")
            .WithTags("Services")
            .WithSummary("Create a new service")
            .Produces<CreateServiceResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        return app;
    }

    private static async Task<IResult> CreateService(
        CreateServiceRequest request,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var command = new CreateServiceCommand(
            Description: request.Description,
            Price: request.Price);

        var result = await mediator.Send(command, cancellationToken);

        var response = new CreateServiceResponse(
            Id: result.Id,
            Description: result.Description,
            Price: result.Price,
            CreatedAt: result.CreatedAt);

        return Results.Created($"/services/{result.Id}", response);
    }
}
