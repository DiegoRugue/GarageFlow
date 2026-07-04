using Mediator;

namespace GarageFlow.Application.WorkOrders.UseCases.StartDiagnosis;

public sealed record StartDiagnosisCommand(Guid WorkOrderId) : IRequest<Unit>;
