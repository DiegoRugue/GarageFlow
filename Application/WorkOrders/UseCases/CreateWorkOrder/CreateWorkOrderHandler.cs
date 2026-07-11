using GarageFlow.SharedKernel.Domain.Exceptions;
using GarageFlow.Application.Customers.Ports;
using GarageFlow.Domain.Customers.ValueObjects;
using GarageFlow.Application.Vehicles.Ports;
using GarageFlow.Domain.Vehicles.ValueObjects;
using GarageFlow.Domain.WorkOrders.Entities;
using GarageFlow.Application.WorkOrders.Ports;
using Mediator;

namespace GarageFlow.Application.WorkOrders.UseCases.CreateWorkOrder;

public sealed class CreateWorkOrderHandler(
    IWorkOrderRepository workOrderRepository,
    ICustomerRepository customerRepository,
    IVehicleRepository vehicleRepository) : IRequestHandler<CreateWorkOrderCommand, CreateWorkOrderResult>
{
    private readonly IWorkOrderRepository _workOrderRepository = workOrderRepository ?? throw new ArgumentNullException(nameof(workOrderRepository));
    private readonly ICustomerRepository _customerRepository = customerRepository ?? throw new ArgumentNullException(nameof(customerRepository));
    private readonly IVehicleRepository _vehicleRepository = vehicleRepository ?? throw new ArgumentNullException(nameof(vehicleRepository));

    public async ValueTask<CreateWorkOrderResult> Handle(CreateWorkOrderCommand request, CancellationToken cancellationToken)
    {
        var customerId = CustomerId.From(request.CustomerId);
        var customer = await _customerRepository.GetByIdAsync(customerId, cancellationToken);
        if (customer is null)
        {
            throw new NotFoundException($"Customer with ID '{request.CustomerId}' was not found.");
        }

        var vehicleId = VehicleId.From(request.VehicleId);
        var vehicle = await _vehicleRepository.GetByIdAsync(vehicleId, cancellationToken);
        if (vehicle is null)
        {
            throw new NotFoundException($"Vehicle with ID '{request.VehicleId}' was not found.");
        }

        if (vehicle.CustomerId != customer.Id)
        {
            throw new BusinessRuleViolationException("Vehicle does not belong to the customer.");
        }

        var workOrder = WorkOrder.Create(customerId, vehicleId);
        await _workOrderRepository.AddAsync(workOrder, cancellationToken);

        return new CreateWorkOrderResult(
            Id: workOrder.Id.Value,
            CustomerId: workOrder.CustomerId.Value,
            VehicleId: workOrder.VehicleId.Value,
            Status: workOrder.Status.ToString(),
            CreatedAt: workOrder.CreatedAt);
    }
}
