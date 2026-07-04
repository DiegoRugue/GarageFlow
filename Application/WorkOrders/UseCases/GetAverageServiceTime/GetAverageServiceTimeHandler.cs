using GarageFlow.SharedKernel.Domain.Exceptions;
using GarageFlow.Domain.Services.ValueObjects;
using GarageFlow.Application.WorkOrders.Ports;
using Mediator;

namespace GarageFlow.Application.WorkOrders.UseCases.GetAverageServiceTime;

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

        var serviceId = request.ServiceId.HasValue ? ServiceId.From(request.ServiceId.Value) : (ServiceId?)null;

        var averageServiceTime = await _workOrderRepository.GetAverageServiceTimeAsync(
            request.From,
            request.To,
            serviceId,
            cancellationToken);

        return new GetAverageServiceTimeResult(
            From: request.From,
            To: request.To,
            ServiceId: request.ServiceId,
            CompletedServicesCount: averageServiceTime.CompletedServicesCount,
            AverageDurationMinutes: averageServiceTime.AverageDurationMinutes);
    }
}
