using GarageFlow.Application.Vehicles.Ports;
using GarageFlow.Application.WorkOrders.UseCases.CreateWorkOrderIntake;
using GarageFlow.Domain.Vehicles.Entities;
using GarageFlow.Domain.Vehicles.ValueObjects;
using Moq;

namespace GarageFlow.Tests.Unit.WorkOrders;

public sealed class ResolveVehicleReferencesHandlerTests
{
    [Fact]
    public async Task ResolveAsync_CreatesMissingReferences()
    {
        var brandRepository = new Mock<IVehicleBrandRepository>(MockBehavior.Strict);
        var modelRepository = new Mock<IVehicleModelRepository>(MockBehavior.Strict);
        var colorRepository = new Mock<IVehicleColorRepository>(MockBehavior.Strict);
        brandRepository.Setup(repository => repository.GetByNameAsync(It.IsAny<VehicleBrandName>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((VehicleBrand?)null);
        brandRepository.Setup(repository => repository.AddAsync(It.IsAny<VehicleBrand>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        modelRepository.Setup(repository => repository.GetByNameAsync(It.IsAny<VehicleBrandId>(), It.IsAny<VehicleModelName>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((VehicleModel?)null);
        modelRepository.Setup(repository => repository.AddAsync(It.IsAny<VehicleModel>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        colorRepository.Setup(repository => repository.GetByNameAsync(It.IsAny<VehicleColorName>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((VehicleColor?)null);
        colorRepository.Setup(repository => repository.AddAsync(It.IsAny<VehicleColor>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var resolver = new ResolveVehicleReferencesHandler(brandRepository.Object, modelRepository.Object, colorRepository.Object);

        var result = await resolver.ResolveAsync(
            VehicleBrandName.Create("Honda"),
            VehicleModelName.Create("Civic"),
            VehicleColorName.Create("Black"),
            CancellationToken.None);

        Assert.Equal("Honda", result.Brand.Name.Value);
        Assert.Equal(result.Brand.Id, result.Model.VehicleBrandId);
        Assert.Equal("Civic", result.Model.Name.Value);
        Assert.Equal("Black", result.Color.Name.Value);
        brandRepository.Verify(repository => repository.AddAsync(result.Brand, It.IsAny<CancellationToken>()), Times.Once);
        modelRepository.Verify(repository => repository.AddAsync(result.Model, It.IsAny<CancellationToken>()), Times.Once);
        colorRepository.Verify(repository => repository.AddAsync(result.Color, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ResolveAsync_ReusesExistingReferences()
    {
        var brand = VehicleBrand.Create("Honda");
        var model = VehicleModel.Create(brand.Id, "Civic");
        var color = VehicleColor.Create("Black");
        var brandRepository = new Mock<IVehicleBrandRepository>(MockBehavior.Strict);
        var modelRepository = new Mock<IVehicleModelRepository>(MockBehavior.Strict);
        var colorRepository = new Mock<IVehicleColorRepository>(MockBehavior.Strict);
        brandRepository.Setup(repository => repository.GetByNameAsync(It.IsAny<VehicleBrandName>(), It.IsAny<CancellationToken>())).ReturnsAsync(brand);
        modelRepository.Setup(repository => repository.GetByNameAsync(brand.Id, It.IsAny<VehicleModelName>(), It.IsAny<CancellationToken>())).ReturnsAsync(model);
        colorRepository.Setup(repository => repository.GetByNameAsync(It.IsAny<VehicleColorName>(), It.IsAny<CancellationToken>())).ReturnsAsync(color);
        var resolver = new ResolveVehicleReferencesHandler(brandRepository.Object, modelRepository.Object, colorRepository.Object);

        var result = await resolver.ResolveAsync(
            VehicleBrandName.Create(" hONDa "),
            VehicleModelName.Create(" cIVic "),
            VehicleColorName.Create(" bLACK "),
            CancellationToken.None);

        Assert.Same(brand, result.Brand);
        Assert.Same(model, result.Model);
        Assert.Same(color, result.Color);
    }

    [Fact]
    public async Task ResolveAsync_PropagatesRepositoryFailure()
    {
        var failure = new InvalidOperationException("repository failure");
        var brandRepository = new Mock<IVehicleBrandRepository>(MockBehavior.Strict);
        brandRepository.Setup(repository => repository.GetByNameAsync(It.IsAny<VehicleBrandName>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(failure);
        var resolver = new ResolveVehicleReferencesHandler(
            brandRepository.Object,
            Mock.Of<IVehicleModelRepository>(),
            Mock.Of<IVehicleColorRepository>());

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => resolver.ResolveAsync(
            VehicleBrandName.Create("Honda"),
            VehicleModelName.Create("Civic"),
            VehicleColorName.Create("Black"),
            CancellationToken.None));

        Assert.Same(failure, exception);
    }
}
