using GarageFlow.Application.Common.Messaging;

namespace GarageFlow.Application.WorkOrders.UseCases.StartDiagnosis;

public sealed record StartDiagnosisCommand(Guid WorkOrderId) : ICommand;
