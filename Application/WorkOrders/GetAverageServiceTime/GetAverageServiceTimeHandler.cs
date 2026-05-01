using GarageFlow.BuildingBlocks.Domain.Exceptions;
using GarageFlow.Domain.WorkOrders.Repositories;
using Mediator;

namespace GarageFlow.Application.WorkOrders.GetAverageServiceTime;

public sealed class GetAverageServiceTimeHandler(
    IWorkOrderRepository workOrderRepository) : IRequestHandler<GetAverageServiceTimeQuery, GetAverageServiceTimeResult>
{
    private readonly IWorkOrderRepository _workOrderRepository = workOrderRepository ?? throw new ArgumentNullException(nameof(workOrderRepository));

    public async ValueTask<GetAverageServiceTimeResult> Handle(GetAverageServiceTimeQuery request, CancellationToken cancellationToken)
    {
        if (request.From >= request.To)
        {
            throw new ValidationException("From must be earlier than To.");
        }

        var averageServiceTime = await _workOrderRepository.GetAverageServiceTimeAsync(
            request.From,
            request.To,
            cancellationToken);

        return new GetAverageServiceTimeResult(
            From: request.From,
            To: request.To,
            CompletedWorkOrdersCount: averageServiceTime.CompletedWorkOrdersCount,
            AverageDurationMinutes: averageServiceTime.AverageDurationMinutes);
    }
}
