using GarageFlow.Application.Customers.CreateCustomer;
using GarageFlow.Application.Customers.DeleteCustomer;
using GarageFlow.Application.Customers.GetCustomerById;
using GarageFlow.Application.Customers.ListCustomers;
using GarageFlow.Application.Customers.UpdateCustomer;
using GarageFlow.SharedKernel.Domain.Exceptions;
using GarageFlow.SharedKernel.Domain.ValueObjects;
using GarageFlow.SharedKernel.Persistence;
using GarageFlow.Domain.Customers.Entities;
using GarageFlow.Domain.Customers.Repositories;
using GarageFlow.Domain.Customers.ValueObjects;
using GarageFlow.Domain.Vehicles.Entities;
using GarageFlow.Domain.Vehicles.Repositories;
using GarageFlow.Tests.Shared.Customers;
using Moq;

namespace GarageFlow.Tests.Unit.Customers;

public class CustomerHandlersTests
{
    [Fact]
    public async Task Handle_ShouldCreateCustomer_WhenDataIsValid()
    {
        var customers = new List<Customer>();
        var repositoryMock = CreateRepositoryMock(customers);
        var unitOfWorkMock = CreateUnitOfWorkMock();
        var handler = new CreateCustomerHandler(repositoryMock.Object, unitOfWorkMock.Object);

        var command = new CreateCustomerCommand(
            TaxDocument: "529.982.247-25",
            FullName: "John Doe",
            Email: "john.doe@example.com",
            PhoneNumber: "11987654321");

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.NotEqual(Guid.Empty, result.Id);
        Assert.Equal("52998224725", result.TaxDocument);
        Assert.Equal("Cpf", result.TaxDocumentType);
        Assert.Equal("John Doe", result.FullName);
        Assert.Equal("john.doe@example.com", result.Email);
        Assert.Equal("11987654321", result.PhoneNumber);

        Assert.Single(customers);
        unitOfWorkMock.Verify(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        unitOfWorkMock.Verify(x => x.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        unitOfWorkMock.Verify(x => x.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldReturnCustomer_WhenIdExists()
    {
        var customer = new CustomerBuilder().Build();
        var repositoryMock = CreateRepositoryMock([customer]);
        var vehicleRepositoryMock = CreateVehicleRepositoryMock([]);

        var handler = new GetCustomerByIdHandler(repositoryMock.Object, vehicleRepositoryMock.Object);
        var query = new GetCustomerByIdQuery(customer.Id.Value);

        var result = await handler.Handle(query, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(customer.Id.Value, result.Id);
        Assert.Equal(customer.TaxDocument.Value, result.TaxDocument);
        Assert.Equal(customer.FullName.Value, result.FullName);
        Assert.Equal(customer.Email.Value, result.Email);
        Assert.Equal(customer.PhoneNumber.Value, result.PhoneNumber);
        Assert.NotNull(result.Vehicles);
        Assert.Empty(result.Vehicles);
    }

    [Fact]
    public async Task Handle_ShouldReturnCustomerWithVehicleList_WhenVehiclesExist()
    {
        var customer = new CustomerBuilder().Build();
        var repositoryMock = CreateRepositoryMock([customer]);
        var vehicleBrand = VehicleBrand.Create("Fiat");
        var vehicleModel = VehicleModel.Create(vehicleBrand.Id, "Uno");
        var vehicleColor = VehicleColor.Create("Black");
        var vehicles = new[]
        {
            new VehicleDetailsReadModel(
                Id: Guid.NewGuid(),
                CustomerId: customer.Id.Value,
                Year: 2024,
                VehicleBrandId: vehicleBrand.Id.Value,
                VehicleBrandName: "Fiat",
                VehicleModelId: vehicleModel.Id.Value,
                VehicleModelName: "Uno",
                VehicleColorId: vehicleColor.Id.Value,
                VehicleColorName: "Black",
                Plate: "ABC1234",
                CreatedAt: DateTime.UtcNow)
        };
        var vehicleRepositoryMock = CreateVehicleRepositoryMock(vehicles);
        var handler = new GetCustomerByIdHandler(repositoryMock.Object, vehicleRepositoryMock.Object);
        var query = new GetCustomerByIdQuery(customer.Id.Value);

        var result = await handler.Handle(query, CancellationToken.None);

        Assert.NotNull(result);
        Assert.NotNull(result.Vehicles);
        Assert.Single(result.Vehicles);
        var vehicle = result.Vehicles[0];
        Assert.Equal(vehicles[0].Id, vehicle.Id);
        Assert.Equal(vehicles[0].Year, vehicle.Year);
        Assert.Equal(vehicles[0].Plate, vehicle.Plate);
        Assert.Equal(vehicles[0].VehicleBrandId, vehicle.VehicleBrandId);
        Assert.Equal(vehicles[0].VehicleBrandName, vehicle.VehicleBrandName);
        Assert.Equal(vehicles[0].VehicleModelId, vehicle.VehicleModelId);
        Assert.Equal(vehicles[0].VehicleModelName, vehicle.VehicleModelName);
        Assert.Equal(vehicles[0].VehicleColorId, vehicle.VehicleColorId);
        Assert.Equal(vehicles[0].VehicleColorName, vehicle.VehicleColorName);
    }

    [Fact]
    public async Task Handle_ShouldListCustomers_WithPagination()
    {
        var firstCustomer = new CustomerBuilder().Build();
        var secondCustomer = new CustomerBuilder()
            .WithTaxDocument("153.509.460-56")
            .WithFullName("Jane Doe")
            .WithEmail("jane.doe@example.com")
            .WithPhoneNumber("21912345678")
            .Build();

        var repositoryMock = CreateRepositoryMock([firstCustomer, secondCustomer]);

        var handler = new ListCustomersHandler(repositoryMock.Object);
        var query = new ListCustomersQuery(Page: 1, PageSize: 1);

        var result = await handler.Handle(query, CancellationToken.None);

        Assert.Single(result.Items);
        Assert.Equal(2, result.TotalCount);
        Assert.Equal(1, result.Page);
        Assert.Equal(1, result.PageSize);
    }

    [Fact]
    public async Task Handle_ShouldThrowValidationException_WhenPaginationBoundsAreInvalid()
    {
        var repositoryMock = CreateRepositoryMock();
        var handler = new ListCustomersHandler(repositoryMock.Object);

        await Assert.ThrowsAsync<ValidationException>(
            async () => await handler.Handle(new ListCustomersQuery(Page: 0, PageSize: 10), CancellationToken.None));

        await Assert.ThrowsAsync<ValidationException>(
            async () => await handler.Handle(new ListCustomersQuery(Page: 1, PageSize: 0), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_ShouldUpdateCustomer_WhenIdExists()
    {
        var customer = new CustomerBuilder().Build();
        var repositoryMock = CreateRepositoryMock([customer]);
        var unitOfWorkMock = CreateUnitOfWorkMock();

        var handler = new UpdateCustomerHandler(repositoryMock.Object, unitOfWorkMock.Object);
        var command = new UpdateCustomerCommand(
            Id: customer.Id.Value,
            FullName: "John Updated",
            Email: "john.updated@example.com",
            PhoneNumber: "11912345678");

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.Equal(customer.Id.Value, result.Id);
        Assert.Equal("John Updated", result.FullName);
        Assert.Equal("john.updated@example.com", result.Email);
        Assert.Equal("11912345678", result.PhoneNumber);

        unitOfWorkMock.Verify(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        unitOfWorkMock.Verify(x => x.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        unitOfWorkMock.Verify(x => x.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldDeleteCustomer_WhenIdExists()
    {
        var customer = new CustomerBuilder().Build();
        var repositoryMock = CreateRepositoryMock([customer]);
        var vehicleRepositoryMock = CreateVehicleRepositoryMock();
        var unitOfWorkMock = CreateUnitOfWorkMock();

        var handler = new DeleteCustomerHandler(repositoryMock.Object, vehicleRepositoryMock.Object, unitOfWorkMock.Object);
        var command = new DeleteCustomerCommand(customer.Id.Value);

        await handler.Handle(command, CancellationToken.None);
        var deleted = await repositoryMock.Object.GetByIdAsync(customer.Id, CancellationToken.None);

        Assert.Null(deleted);
        unitOfWorkMock.Verify(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        unitOfWorkMock.Verify(x => x.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        unitOfWorkMock.Verify(x => x.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
        repositoryMock.Verify(x => x.Remove(It.Is<Customer>(c => c.Id == customer.Id)), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldThrowBusinessRuleViolationException_WhenDeletingCustomerWithVehicles()
    {
        var customer = new CustomerBuilder().Build();
        var repositoryMock = CreateRepositoryMock([customer]);
        var vehicleRepositoryMock = CreateVehicleRepositoryMock(existsByCustomerId: true);
        var unitOfWorkMock = CreateUnitOfWorkMock();
        var handler = new DeleteCustomerHandler(repositoryMock.Object, vehicleRepositoryMock.Object, unitOfWorkMock.Object);
        var command = new DeleteCustomerCommand(customer.Id.Value);

        await Assert.ThrowsAsync<BusinessRuleViolationException>(
            async () => await handler.Handle(command, CancellationToken.None));

        unitOfWorkMock.Verify(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
        vehicleRepositoryMock.Verify(
            x => x.ExistsByCustomerIdAsync(It.Is<CustomerId>(id => id == customer.Id), It.IsAny<CancellationToken>()),
            Times.Once);
        repositoryMock.Verify(x => x.Remove(It.IsAny<Customer>()), Times.Never);
    }

    private static Mock<ICustomerRepository> CreateRepositoryMock(List<Customer>? initialCustomers = null)
    {
        var customers = initialCustomers ?? [];
        var repositoryMock = new Mock<ICustomerRepository>();

        repositoryMock
            .Setup(x => x.GetByIdAsync(It.IsAny<CustomerId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CustomerId id, CancellationToken _) => customers.FirstOrDefault(c => c.Id == id));

        repositoryMock
            .Setup(x => x.ListAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((int page, int pageSize, CancellationToken _) =>
            {
                var totalCount = customers.Count;
                var items = customers
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToList();

                return ((IReadOnlyList<Customer>)items, totalCount);
            });

        repositoryMock
            .Setup(x => x.AddAsync(It.IsAny<Customer>(), It.IsAny<CancellationToken>()))
            .Returns((Customer customer, CancellationToken _) =>
            {
                customers.Add(customer);
                return Task.CompletedTask;
            });

        repositoryMock
            .Setup(x => x.ExistsByTaxDocumentAsync(It.IsAny<TaxDocument>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((TaxDocument taxDocument, CancellationToken _) =>
                customers.Any(c => c.TaxDocument.Value == taxDocument.Value));

        repositoryMock
            .Setup(x => x.Remove(It.IsAny<Customer>()))
            .Callback((Customer customer) => customers.RemoveAll(c => c.Id == customer.Id));

        return repositoryMock;
    }

    private static Mock<IVehicleRepository> CreateVehicleRepositoryMock(
        IReadOnlyList<VehicleDetailsReadModel>? initialVehicles = null,
        bool existsByCustomerId = false)
    {
        var repositoryMock = new Mock<IVehicleRepository>();
        var vehicles = initialVehicles ?? [];

        repositoryMock
            .Setup(x => x.ExistsByCustomerIdAsync(It.IsAny<CustomerId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(existsByCustomerId);

        repositoryMock
            .Setup(x => x.ListDetailsByCustomerIdAsync(It.IsAny<CustomerId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(vehicles);

        return repositoryMock;
    }

    private static Mock<IUnitOfWork> CreateUnitOfWorkMock()
    {
        var unitOfWorkMock = new Mock<IUnitOfWork>();

        unitOfWorkMock
            .Setup(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        unitOfWorkMock
            .Setup(x => x.CommitTransactionAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        unitOfWorkMock
            .Setup(x => x.RollbackTransactionAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        unitOfWorkMock
            .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        return unitOfWorkMock;
    }
}
