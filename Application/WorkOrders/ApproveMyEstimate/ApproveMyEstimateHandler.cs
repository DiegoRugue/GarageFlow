using GarageFlow.BuildingBlocks.Domain.Exceptions;
using GarageFlow.BuildingBlocks.Persistence;
using GarageFlow.Domain.Users.Repositories;
using GarageFlow.Domain.WorkOrders.Repositories;
using GarageFlow.Domain.WorkOrders.ValueObjects;
using Mediator;

namespace GarageFlow.Application.WorkOrders.ApproveMyEstimate;

public sealed class ApproveMyEstimateHandler(
    IUserRepository userRepository,
    IWorkOrderRepository workOrderRepository,
    IUnitOfWork unitOfWork) : IRequestHandler<ApproveMyEstimateCommand, Unit>
{
    private readonly IUserRepository _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
    private readonly IWorkOrderRepository _workOrderRepository = workOrderRepository ?? throw new ArgumentNullException(nameof(workOrderRepository));
    private readonly IUnitOfWork _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));

    public async ValueTask<Unit> Handle(ApproveMyEstimateCommand request, CancellationToken cancellationToken)
    {
        var customerId = await CustomerWorkOrderAccess.GetRequiredCustomerIdAsync(_userRepository, request.UserId, cancellationToken);
        var workOrderId = WorkOrderId.From(request.WorkOrderId);
        await _unitOfWork.BeginTransactionAsync(cancellationToken);

        try
        {
            var workOrder = await CustomerWorkOrderAccess.GetRequiredEstimateMutationWorkOrderAsync(
                _workOrderRepository,
                workOrderId,
                customerId,
                request.WorkOrderId,
                cancellationToken);
            workOrder.ApproveEstimate(EstimateId.From(request.EstimateId));
            await _unitOfWork.CommitTransactionAsync(cancellationToken);
            return Unit.Value;
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            throw;
        }
    }
}
