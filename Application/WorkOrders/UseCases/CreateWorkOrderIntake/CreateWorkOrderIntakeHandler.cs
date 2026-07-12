using System.Text.Json;
using GarageFlow.Application.Customers.Ports;
using GarageFlow.Application.InventoryItems.Common;
using GarageFlow.Application.InventoryItems.Ports;
using GarageFlow.Application.Services.Ports;
using GarageFlow.Application.Vehicles.Ports;
using GarageFlow.Application.WorkOrders.Ports;
using GarageFlow.Domain.Customers.Entities;
using GarageFlow.Domain.InventoryItems.Entities;
using GarageFlow.Domain.InventoryItems.ValueObjects;
using GarageFlow.Domain.Services.Entities;
using GarageFlow.Domain.Vehicles.Entities;
using GarageFlow.Domain.Vehicles.ValueObjects;
using GarageFlow.Domain.WorkOrders.Entities;
using GarageFlow.Domain.WorkOrders.ValueObjects;
using GarageFlow.SharedKernel.Domain.Exceptions;
using GarageFlow.SharedKernel.Domain.ValueObjects;
using Mediator;

namespace GarageFlow.Application.WorkOrders.UseCases.CreateWorkOrderIntake;

public sealed class CreateWorkOrderIntakeHandler(
    IWorkOrderIntakeRequestStore requestStore,
    ICustomerRepository customerRepository,
    IVehicleRepository vehicleRepository,
    IVehicleBrandRepository brandRepository,
    IVehicleModelRepository modelRepository,
    IVehicleColorRepository colorRepository,
    IServiceRepository serviceRepository,
    IInventoryItemRepository inventoryItemRepository,
    IWorkOrderRepository workOrderRepository)
    : IRequestHandler<CreateWorkOrderIntakeCommand, CreateWorkOrderIntakeResult>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly IWorkOrderIntakeRequestStore _requestStore = requestStore ?? throw new ArgumentNullException(nameof(requestStore));
    private readonly ICustomerRepository _customerRepository = customerRepository ?? throw new ArgumentNullException(nameof(customerRepository));
    private readonly IVehicleRepository _vehicleRepository = vehicleRepository ?? throw new ArgumentNullException(nameof(vehicleRepository));
    private readonly IVehicleBrandRepository _brandRepository = brandRepository ?? throw new ArgumentNullException(nameof(brandRepository));
    private readonly IVehicleModelRepository _modelRepository = modelRepository ?? throw new ArgumentNullException(nameof(modelRepository));
    private readonly IVehicleColorRepository _colorRepository = colorRepository ?? throw new ArgumentNullException(nameof(colorRepository));
    private readonly IServiceRepository _serviceRepository = serviceRepository ?? throw new ArgumentNullException(nameof(serviceRepository));
    private readonly IInventoryItemRepository _inventoryItemRepository = inventoryItemRepository ?? throw new ArgumentNullException(nameof(inventoryItemRepository));
    private readonly IWorkOrderRepository _workOrderRepository = workOrderRepository ?? throw new ArgumentNullException(nameof(workOrderRepository));

    public async ValueTask<CreateWorkOrderIntakeResult> Handle(
        CreateWorkOrderIntakeCommand request,
        CancellationToken cancellationToken)
    {
        var payload = Validate(request);
        var payloadHash = IntakePayloadCanonicalizer.Compute(payload);
        var claim = await _requestStore.ClaimAsync(request.RequestId, payloadHash, cancellationToken);

        if (claim.State == IntakeRequestClaimState.Completed)
        {
            if (!string.Equals(claim.PayloadHash, payloadHash, StringComparison.Ordinal))
            {
                throw new BusinessRuleViolationException("The request identifier was already used with a different payload.");
            }

            if (string.IsNullOrWhiteSpace(claim.ResponseJson))
            {
                throw new BusinessRuleViolationException("The completed intake request has no stored response.");
            }

            var stored = JsonSerializer.Deserialize<StoredWorkOrderIntakeResponse>(claim.ResponseJson, JsonOptions)
                ?? throw new BusinessRuleViolationException("The completed intake request has an invalid stored response.");
            return ToResult(stored, true);
        }

        if (await _customerRepository.ExistsByTaxDocumentAsync(payload.TaxDocument, cancellationToken))
        {
            throw new BusinessRuleViolationException("A customer with the same tax document already exists.");
        }

        if (await _vehicleRepository.ExistsByLicensePlateAsync(payload.Plate, cancellationToken))
        {
            throw new BusinessRuleViolationException("A vehicle with the same license plate already exists.");
        }

        var brand = await _brandRepository.GetByNameAsync(payload.Brand, cancellationToken);
        if (brand is null)
        {
            brand = VehicleBrand.Create(payload.Brand.Value);
            await _brandRepository.AddAsync(brand, cancellationToken);
        }

        var model = await _modelRepository.GetByNameAsync(brand.Id, payload.Model, cancellationToken);
        if (model is null)
        {
            model = VehicleModel.Create(brand.Id, payload.Model.Value);
            await _modelRepository.AddAsync(model, cancellationToken);
        }

        var color = await _colorRepository.GetByNameAsync(payload.Color, cancellationToken);
        if (color is null)
        {
            color = VehicleColor.Create(payload.Color.Value);
            await _colorRepository.AddAsync(color, cancellationToken);
        }

        var customer = Customer.Create(payload.TaxDocument, payload.FullName, payload.Email, payload.PhoneNumber);
        await _customerRepository.AddAsync(customer, cancellationToken);

        var vehicle = Vehicle.Create(
            customer.Id,
            payload.Year,
            brand.Id,
            model.Id,
            color.Id,
            payload.Plate);
        await _vehicleRepository.AddAsync(vehicle, cancellationToken);

        var services = new List<Service>(payload.Services.Count);
        foreach (var input in payload.Services)
        {
            var service = Service.Create(input.Description, input.Price);
            services.Add(service);
            await _serviceRepository.AddAsync(service, cancellationToken);
        }

        var inventoryItems = new List<(InventoryItem Item, EstimateItemQuantity Quantity)>(payload.InventoryItems.Count);
        foreach (var input in payload.InventoryItems)
        {
            var item = InventoryItem.Create(
                input.Name,
                input.Description,
                input.Type,
                input.Cost,
                input.Price,
                input.StockQuantity);
            item.DecreaseStock(input.Quantity.Value);
            inventoryItems.Add((item, input.Quantity));
            await _inventoryItemRepository.AddAsync(item, cancellationToken);
        }

        var workOrder = WorkOrder.Create(customer.Id, vehicle.Id);
        await _workOrderRepository.AddAsync(workOrder, cancellationToken);
        var estimate = workOrder.CreateEstimate();
        foreach (var service in services)
        {
            workOrder.AddServiceLine(estimate.Id, service.Id, service.Description, service.Price);
        }

        foreach (var (item, quantity) in inventoryItems)
        {
            workOrder.AddInventoryLine(
                estimate.Id,
                item.Id,
                item.Description,
                quantity,
                item.Cost,
                item.Price);
        }

        var storedResponse = new StoredWorkOrderIntakeResponse(
            workOrder.Id.Value,
            customer.Id.Value,
            vehicle.Id.Value,
            estimate.Id.Value,
            services.Select(service => service.Id.Value).ToList(),
            inventoryItems.Select(item => item.Item.Id.Value).ToList(),
            workOrder.Status.ToString(),
            workOrder.CreatedAt);
        var responseJson = JsonSerializer.Serialize(storedResponse, JsonOptions);
        await _requestStore.CompleteAsync(
            request.RequestId,
            workOrder.Id.Value,
            responseJson,
            DateTime.UtcNow,
            cancellationToken);

        return ToResult(storedResponse, false);
    }

    private static ValidatedIntakePayload Validate(CreateWorkOrderIntakeCommand request)
    {
        if (request.RequestId == Guid.Empty)
        {
            throw new ValidationException("Request identifier cannot be empty.");
        }

        if (request.Customer is null)
        {
            throw new ValidationException("Customer input cannot be null.");
        }

        if (request.Vehicle is null)
        {
            throw new ValidationException("Vehicle input cannot be null.");
        }

        if (request.Services is null || request.InventoryItems is null)
        {
            throw new ValidationException("Services and inventory item collections cannot be null.");
        }

        var taxDocument = TaxDocument.Create(request.Customer.TaxDocument);
        var fullName = FullName.Create(request.Customer.FullName);
        var email = Email.Create(request.Customer.Email);
        var phoneNumber = PhoneNumber.Create(request.Customer.PhoneNumber);
        var plate = LicensePlate.Create(request.Vehicle.Plate);
        var year = VehicleYear.Create(request.Vehicle.Year);
        var brand = VehicleBrandName.Create(request.Vehicle.Brand);
        var model = VehicleModelName.Create(request.Vehicle.Model);
        var color = VehicleColorName.Create(request.Vehicle.Color);
        var services = request.Services
            .Select(input => new ValidatedIntakeService(
                Description.Create(input.Description),
                Price.Create(input.Price)))
            .ToList();
        var inventoryItems = request.InventoryItems
            .Select(input => new ValidatedIntakeInventoryItem(
                InventoryItemName.Create(input.Name),
                Description.Create(input.Description),
                InventoryItemTypeParser.Parse(input.Type),
                Price.Create(input.Cost),
                Price.Create(input.Price),
                InventoryItemStockQuantity.Create(input.StockQuantity),
                EstimateItemQuantity.Create(input.Quantity)))
            .ToList();

        if (services.Count == 0)
        {
            throw new ValidationException("At least one service is required.");
        }

        EnsureNoDuplicates(
            services.Select(service => service.Description.Value),
            "Service descriptions must be unique.");
        EnsureNoDuplicates(
            inventoryItems.Select(item => item.Name.Value),
            "Inventory item names must be unique.");

        return new ValidatedIntakePayload(
            taxDocument,
            fullName,
            email,
            phoneNumber,
            plate,
            year,
            brand,
            model,
            color,
            services,
            inventoryItems);
    }

    private static void EnsureNoDuplicates(IEnumerable<string> values, string message)
    {
        var unique = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (values.Any(value => !unique.Add(value)))
        {
            throw new ValidationException(message);
        }
    }

    private static CreateWorkOrderIntakeResult ToResult(StoredWorkOrderIntakeResponse response, bool isReplay) => new(
        isReplay,
        response.WorkOrderId,
        response.CustomerId,
        response.VehicleId,
        response.EstimateId,
        response.ServiceIds,
        response.InventoryItemIds,
        response.Status,
        response.CreatedAt);
}
