using Mediator;

namespace GarageFlow.Application.WorkOrders.StartDiagnosis;

public sealed record StartDiagnosisCommand(Guid WorkOrderId) : IRequest<Unit>;
