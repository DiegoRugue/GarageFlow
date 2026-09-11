using GarageFlow.SharedKernel.Domain.Exceptions;
using GarageFlow.Application.Customers.Ports;
using GarageFlow.Domain.Customers.ValueObjects;
using GarageFlow.Application.Vehicles.Ports;
using Mediator;

namespace GarageFlow.Application.Customers.UseCases.GetCustomerById;

public sealed class GetCustomerByIdHandler(
    ICustomerRepository customerRepository,
    IVehicleQueries vehicleQueries) : IRequestHandler<GetCustomerByIdQuery, CustomerDto>
{
    private readonly ICustomerRepository _customerRepository = customerRepository ?? throw new ArgumentNullException(nameof(customerRepository));
    private readonly IVehicleQueries _vehicleQueries = vehicleQueries ?? throw new ArgumentNullException(nameof(vehicleQueries));

    public async ValueTask<CustomerDto> Handle(GetCustomerByIdQuery request, CancellationToken cancellationToken)
    {
        var customerId = CustomerId.From(request.Id);
        var customer = await _customerRepository.GetByIdAsync(customerId, cancellationToken);

        if (customer is null)
        {
            throw new NotFoundException($"Customer with ID '{request.Id}' was not found.");
        }

        var vehicleDtos = (await _vehicleQueries.ListDetailsByCustomerIdAsync(customerId, cancellationToken))
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
            Status: customer.Status.ToString(),
            CreatedAt: customer.CreatedAt,
            Vehicles: vehicleDtos);
    }
}
