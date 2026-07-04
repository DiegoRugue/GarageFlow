using GarageFlow.SharedKernel.Domain.Exceptions;
using GarageFlow.Domain.Customers.Repositories;
using GarageFlow.Domain.Customers.ValueObjects;
using GarageFlow.Domain.Vehicles.Repositories;
using Mediator;

namespace GarageFlow.Application.Customers.UseCases.GetCustomerById;

public sealed class GetCustomerByIdHandler(
    ICustomerRepository customerRepository,
    IVehicleRepository vehicleRepository) : IRequestHandler<GetCustomerByIdQuery, CustomerDto>
{
    private readonly ICustomerRepository _customerRepository = customerRepository ?? throw new ArgumentNullException(nameof(customerRepository));
    private readonly IVehicleRepository _vehicleRepository = vehicleRepository ?? throw new ArgumentNullException(nameof(vehicleRepository));

    public async ValueTask<CustomerDto> Handle(GetCustomerByIdQuery request, CancellationToken cancellationToken)
    {
        var customerId = CustomerId.From(request.Id);
        var customer = await _customerRepository.GetByIdAsync(customerId, cancellationToken);

        if (customer is null)
        {
            throw new NotFoundException($"Customer with ID '{request.Id}' was not found.");
        }

        var vehicleDtos = (await _vehicleRepository.ListDetailsByCustomerIdAsync(customerId, cancellationToken))
            .Select(vehicle => new CustomerVehicleDto(
                Id: vehicle.Id,
                Year: vehicle.Year,
                Plate: vehicle.Plate,
                VehicleBrandId: vehicle.VehicleBrandId,
                VehicleBrandName: vehicle.VehicleBrandName,
                VehicleModelId: vehicle.VehicleModelId,
                VehicleModelName: vehicle.VehicleModelName,
                VehicleColorId: vehicle.VehicleColorId,
                VehicleColorName: vehicle.VehicleColorName,
                CreatedAt: vehicle.CreatedAt))
            .ToList();

        return new CustomerDto(
            Id: customer.Id.Value,
            TaxDocument: customer.TaxDocument.Value,
            TaxDocumentType: customer.TaxDocument.DocumentType.ToString(),
            FullName: customer.FullName.Value,
            Email: customer.Email.Value,
            PhoneNumber: customer.PhoneNumber.Value,
            CreatedAt: customer.CreatedAt,
            Vehicles: vehicleDtos);
    }
}
