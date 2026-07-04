using GarageFlow.Application.Services.UseCases.CreateService;
using GarageFlow.Application.Services.UseCases.DeleteService;
using GarageFlow.Application.Services.UseCases.GetServiceById;
using GarageFlow.Application.Services.UseCases.ListServices;
using GarageFlow.Application.Services.UseCases.UpdateService;
using GarageFlow.SharedKernel.Domain.Exceptions;
using GarageFlow.SharedKernel.Domain.ValueObjects;
using GarageFlow.SharedKernel.Persistence;
using GarageFlow.Domain.Services.Entities;
using GarageFlow.Application.Services.Ports;
using GarageFlow.Domain.Services.ValueObjects;
using GarageFlow.Tests.Shared.Services;
using Moq;

namespace GarageFlow.Tests.Unit.Services;

public class ServiceHandlersTests
{
    [Fact]
    public async Task Handle_ShouldCreateService_WhenDataIsValid()
    {
        var services = new List<Service>();
        var repositoryMock = CreateRepositoryMock(services);
        var unitOfWorkMock = CreateUnitOfWorkMock();
        var handler = new CreateServiceHandler(repositoryMock.Object);

        var command = new CreateServiceCommand(
            Description: "Oil change",
            Price: 129.90m);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.NotEqual(Guid.Empty, result.Id);
        Assert.Equal("Oil change", result.Description);
        Assert.Equal(129.90m, result.Price);
        Assert.Single(services);
    }

    [Fact]
    public async Task Handle_ShouldReturnService_WhenIdExists()
    {
        var service = new ServiceBuilder().Build();
        var repositoryMock = CreateRepositoryMock([service]);

        var handler = new GetServiceByIdHandler(repositoryMock.Object);
        var query = new GetServiceByIdQuery(service.Id.Value);

        var result = await handler.Handle(query, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(service.Id.Value, result.Id);
        Assert.Equal(service.Description.Value, result.Description);
        Assert.Equal(service.Price.Value, result.Price);
    }

    [Fact]
    public async Task Handle_ShouldListServices_WithPagination()
    {
        var firstService = new ServiceBuilder().Build();
        var secondService = new ServiceBuilder()
            .WithDescription("Wheel alignment")
            .WithPrice(89.50m)
            .Build();

        var repositoryMock = CreateRepositoryMock([firstService, secondService]);

        var handler = new ListServicesHandler(repositoryMock.Object);
        var query = new ListServicesQuery(Page: 1, PageSize: 1);

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
        var handler = new ListServicesHandler(repositoryMock.Object);

        await Assert.ThrowsAsync<ValidationException>(
            async () => await handler.Handle(new ListServicesQuery(Page: 0, PageSize: 10), CancellationToken.None));

        await Assert.ThrowsAsync<ValidationException>(
            async () => await handler.Handle(new ListServicesQuery(Page: 1, PageSize: 0), CancellationToken.None));

        await Assert.ThrowsAsync<ValidationException>(
            async () => await handler.Handle(new ListServicesQuery(Page: 1, PageSize: 101), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_ShouldUpdateService_WhenIdExists()
    {
        var service = new ServiceBuilder().Build();
        var repositoryMock = CreateRepositoryMock([service]);
        var unitOfWorkMock = CreateUnitOfWorkMock();

        var handler = new UpdateServiceHandler(repositoryMock.Object);
        var command = new UpdateServiceCommand(
            Id: service.Id.Value,
            Description: "Premium oil change",
            Price: 199.90m);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.Equal(service.Id.Value, result.Id);
        Assert.Equal("Premium oil change", result.Description);
        Assert.Equal(199.90m, result.Price);
    }

    [Fact]
    public async Task Handle_ShouldThrowNotFoundException_WhenUpdatingMissingService()
    {
        var repositoryMock = CreateRepositoryMock();
        var unitOfWorkMock = CreateUnitOfWorkMock();
        var handler = new UpdateServiceHandler(repositoryMock.Object);

        await Assert.ThrowsAsync<NotFoundException>(
            async () => await handler.Handle(new UpdateServiceCommand(Guid.NewGuid(), "Premium oil change", 199.90m), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_ShouldDeleteService_WhenIdExists()
    {
        var service = new ServiceBuilder().Build();
        var repositoryMock = CreateRepositoryMock([service]);
        var unitOfWorkMock = CreateUnitOfWorkMock();

        var handler = new DeleteServiceHandler(repositoryMock.Object);
        var command = new DeleteServiceCommand(service.Id.Value);

        await handler.Handle(command, CancellationToken.None);
        var deleted = await repositoryMock.Object.GetByIdAsync(service.Id, CancellationToken.None);

        Assert.Null(deleted);
        repositoryMock.Verify(x => x.Remove(It.Is<Service>(s => s.Id == service.Id)), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldThrowNotFoundException_WhenDeletingMissingService()
    {
        var repositoryMock = CreateRepositoryMock();
        var unitOfWorkMock = CreateUnitOfWorkMock();
        var handler = new DeleteServiceHandler(repositoryMock.Object);

        await Assert.ThrowsAsync<NotFoundException>(
            async () => await handler.Handle(new DeleteServiceCommand(Guid.NewGuid()), CancellationToken.None));
        repositoryMock.Verify(x => x.Remove(It.IsAny<Service>()), Times.Never);
    }

    private static Mock<IServiceRepository> CreateRepositoryMock(List<Service>? initialServices = null)
    {
        var services = initialServices ?? [];
        var repositoryMock = new Mock<IServiceRepository>();

        repositoryMock
            .Setup(x => x.GetByIdAsync(It.IsAny<ServiceId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ServiceId id, CancellationToken _) => services.FirstOrDefault(service => service.Id == id));

        repositoryMock
            .Setup(x => x.ListAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((int page, int pageSize, CancellationToken _) =>
            {
                var totalCount = services.Count;
                var items = services
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToList();

                return ((IReadOnlyList<Service>)items, totalCount);
            });

        repositoryMock
            .Setup(x => x.AddAsync(It.IsAny<Service>(), It.IsAny<CancellationToken>()))
            .Returns((Service service, CancellationToken _) =>
            {
                services.Add(service);
                return Task.CompletedTask;
            });

        repositoryMock
            .Setup(x => x.Remove(It.IsAny<Service>()))
            .Callback((Service service) => services.RemoveAll(item => item.Id == service.Id));

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
