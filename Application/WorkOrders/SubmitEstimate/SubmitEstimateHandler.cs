using GarageFlow.Application.WorkOrders.Abstractions;
using GarageFlow.BuildingBlocks.Domain.Exceptions;
using GarageFlow.BuildingBlocks.Persistence;
using GarageFlow.Domain.WorkOrders.Enums;
using GarageFlow.Domain.WorkOrders.Repositories;
using GarageFlow.Domain.WorkOrders.ValueObjects;
using Mediator;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace GarageFlow.Application.WorkOrders.SubmitEstimate;

public sealed class SubmitEstimateHandler(
    IWorkOrderRepository workOrderRepository,
    IUnitOfWork unitOfWork,
    ICustomerApprovalEmailSender emailSender,
    ILogger<SubmitEstimateHandler>? logger = null) : IRequestHandler<SubmitEstimateCommand, Unit>
{
    private static readonly Action<ILogger, Guid, Guid, Exception?> LogApprovalEmailFailure = LoggerMessage.Define<Guid, Guid>(
        LogLevel.Error,
        new EventId(1, nameof(LogApprovalEmailFailure)),
        "Failed to send approval email after commit for work order {WorkOrderId} and estimate {EstimateId}.");

    private readonly IWorkOrderRepository _workOrderRepository = workOrderRepository ?? throw new ArgumentNullException(nameof(workOrderRepository));
    private readonly IUnitOfWork _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    private readonly ICustomerApprovalEmailSender _emailSender = emailSender ?? throw new ArgumentNullException(nameof(emailSender));
    private readonly ILogger<SubmitEstimateHandler> _logger = logger ?? NullLogger<SubmitEstimateHandler>.Instance;

    public async ValueTask<Unit> Handle(SubmitEstimateCommand request, CancellationToken cancellationToken)
    {
        var workOrderId = WorkOrderId.From(request.WorkOrderId);
        var estimateId = EstimateId.From(request.EstimateId);
        var shouldSendApprovalEmail = false;
        var customerIdForApprovalEmail = Guid.Empty;

        await _unitOfWork.BeginTransactionAsync(cancellationToken);

        try
        {
            var workOrder = await _workOrderRepository.GetByIdForEstimateMutationAsync(workOrderId, cancellationToken);
            if (workOrder is null)
            {
                throw new NotFoundException($"Work order with ID '{request.WorkOrderId}' was not found.");
            }

            var previousStatus = workOrder.Status;
            workOrder.SubmitEstimate(estimateId);
            if (previousStatus != WorkOrderStatus.WaitingApproval && workOrder.Status == WorkOrderStatus.WaitingApproval)
            {
                shouldSendApprovalEmail = true;
                customerIdForApprovalEmail = workOrder.CustomerId.Value;
            }

            await _unitOfWork.CommitTransactionAsync(cancellationToken);
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            throw;
        }

        if (shouldSendApprovalEmail)
        {
            try
            {
                await _emailSender.SendEstimateWaitingApprovalAsync(
                    request.WorkOrderId,
                    request.EstimateId,
                    customerIdForApprovalEmail,
                    cancellationToken);
            }
            catch (Exception ex)
            {
                LogApprovalEmailFailure(
                    _logger,
                    request.WorkOrderId,
                    request.EstimateId,
                    ex);
            }
        }

        return Unit.Value;
    }
}
