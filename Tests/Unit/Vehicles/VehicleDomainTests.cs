using GarageFlow.BuildingBlocks.Domain.Exceptions;
using GarageFlow.Domain.Customers.ValueObjects;
using GarageFlow.Domain.Vehicles.Entities;
using GarageFlow.Domain.Vehicles.Events;
using GarageFlow.Domain.Vehicles.ValueObjects;

namespace GarageFlow.Tests.Unit.Vehicles;

public class VehicleDomainTests
{
    [Fact]
    public void VehicleId_From_WithEmptyGuid_ShouldThrowValidationException()
    {
        Assert.Throws<ValidationException>(() => VehicleId.From(Guid.Empty));
    }

    [Fact]
    public void VehicleBrandId_From_WithEmptyGuid_ShouldThrowValidationException()
    {
        Assert.Throws<ValidationException>(() => VehicleBrandId.From(Guid.Empty));
    }

    [Fact]
    public void VehicleModelId_From_WithEmptyGuid_ShouldThrowValidationException()
    {
        Assert.Throws<ValidationException>(() => VehicleModelId.From(Guid.Empty));
    }

    [Fact]
    public void VehicleColorId_From_WithEmptyGuid_ShouldThrowValidationException()
    {
        Assert.Throws<ValidationException>(() => VehicleColorId.From(Guid.Empty));
    }

    [Fact]
    public void VehicleBrandName_Create_ShouldNormalize()
    {
        var name = VehicleBrandName.Create("  Fiat  ");

        Assert.Equal("Fiat", name.Value);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void VehicleBrandName_Create_WithEmptyValue_ShouldThrowValidationException(string value)
    {
        Assert.Throws<ValidationException>(() => VehicleBrandName.Create(value));
    }

    [Fact]
    public void VehicleBrandName_Create_WithValueLongerThanMaxLength_ShouldThrowValidationException()
    {
        var value = new string('A', VehicleBrandName.MaxLength + 1);

        Assert.Throws<ValidationException>(() => VehicleBrandName.Create(value));
    }

    [Fact]
    public void VehicleModelName_Create_ShouldNormalize()
    {
        var name = VehicleModelName.Create("  Mobi  ");

        Assert.Equal("Mobi", name.Value);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void VehicleModelName_Create_WithEmptyValue_ShouldThrowValidationException(string value)
    {
        Assert.Throws<ValidationException>(() => VehicleModelName.Create(value));
    }

    [Fact]
    public void VehicleModelName_Create_WithValueLongerThanMaxLength_ShouldThrowValidationException()
    {
        var value = new string('A', VehicleModelName.MaxLength + 1);

        Assert.Throws<ValidationException>(() => VehicleModelName.Create(value));
    }

    [Fact]
    public void VehicleColorName_Create_ShouldNormalize()
    {
        var name = VehicleColorName.Create("  Black  ");

        Assert.Equal("Black", name.Value);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void VehicleColorName_Create_WithEmptyValue_ShouldThrowValidationException(string value)
    {
        Assert.Throws<ValidationException>(() => VehicleColorName.Create(value));
    }

    [Fact]
    public void VehicleColorName_Create_WithValueLongerThanMaxLength_ShouldThrowValidationException()
    {
        var value = new string('A', VehicleColorName.MaxLength + 1);

        Assert.Throws<ValidationException>(() => VehicleColorName.Create(value));
    }

    [Fact]
    public void LicensePlate_Create_WithOldFormat_ShouldNormalize()
    {
        var licensePlate = LicensePlate.Create("abc-1234");

        Assert.Equal("ABC1234", licensePlate.Value);
    }

    [Fact]
    public void LicensePlate_Create_WithMercosulFormat_ShouldNormalize()
    {
        var licensePlate = LicensePlate.Create("abc1d23");

        Assert.Equal("ABC1D23", licensePlate.Value);
    }

    [Fact]
    public void VehicleYear_Create_WithPositiveValue_ShouldSucceed()
    {
        var year = VehicleYear.Create(2024);

        Assert.Equal(2024, year.Value);
    }

    [Fact]
    public void VehicleYear_Create_WithInvalidValue_ShouldThrowValidationException()
    {
        Assert.Throws<ValidationException>(() => VehicleYear.Create(0));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("AB12345")]
    [InlineData("ABCD123")]
    [InlineData("ABC12D3")]
    [InlineData("ABC-12#4")]
    public void LicensePlate_Create_WithInvalidValue_ShouldThrowValidationException(string value)
    {
        Assert.Throws<ValidationException>(() => LicensePlate.Create(value));
    }

    [Fact]
    public void VehicleBrand_Create_ShouldRaiseVehicleBrandCreatedEvent()
    {
        var brand = VehicleBrand.Create("  Fiat  ");

        var createdEvent = Assert.Single(brand.DomainEvents.OfType<VehicleBrandCreated>());
        Assert.Equal(brand.Id, createdEvent.VehicleBrandId);
        Assert.Equal("Fiat", brand.Name.Value);
        Assert.Equal(brand.Name.Value, createdEvent.Name);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void VehicleBrand_Create_WithEmptyName_ShouldThrowValidationException(string value)
    {
        Assert.Throws<ValidationException>(() => VehicleBrand.Create(value));
    }

    [Fact]
    public void VehicleBrand_Create_WithNameLongerThanMaxLength_ShouldThrowValidationException()
    {
        var value = new string('A', VehicleBrandName.MaxLength + 1);

        Assert.Throws<ValidationException>(() => VehicleBrand.Create(value));
    }

    [Fact]
    public void VehicleBrand_Update_ShouldRaiseVehicleBrandUpdatedEvent()
    {
        var brand = VehicleBrand.Create("Fiat");
        var originalUpdatedAt = brand.UpdatedAt;
        var beforeUpdate = DateTime.UtcNow;

        brand.Update("  Ford  ");

        var updatedEvent = Assert.Single(brand.DomainEvents.OfType<VehicleBrandUpdated>());
        Assert.Equal(brand.Id, updatedEvent.VehicleBrandId);
        Assert.Equal("Ford", updatedEvent.Name);
        Assert.Equal("Ford", brand.Name);
        Assert.True(brand.UpdatedAt >= beforeUpdate);
        Assert.True(brand.UpdatedAt >= originalUpdatedAt);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void VehicleBrand_Update_WithEmptyName_ShouldThrowValidationException(string value)
    {
        var brand = VehicleBrand.Create("Fiat");

        Assert.Throws<ValidationException>(() => brand.Update(value));
    }

    [Fact]
    public void VehicleBrand_Update_WithNameLongerThanMaxLength_ShouldThrowValidationException()
    {
        var brand = VehicleBrand.Create("Fiat");
        var value = new string('A', VehicleBrandName.MaxLength + 1);

        Assert.Throws<ValidationException>(() => brand.Update(value));
    }

    [Fact]
    public void VehicleBrand_Delete_ShouldRaiseVehicleBrandDeletedEvent()
    {
        var brand = VehicleBrand.Create("Fiat");
        var originalUpdatedAt = brand.UpdatedAt;
        var beforeDelete = DateTime.UtcNow;

        brand.Delete();

        var deletedEvent = Assert.Single(brand.DomainEvents.OfType<VehicleBrandDeleted>());
        Assert.Equal(brand.Id, deletedEvent.VehicleBrandId);
        Assert.Equal(brand.Name.Value, deletedEvent.Name);
        Assert.True(brand.UpdatedAt >= beforeDelete);
        Assert.True(brand.UpdatedAt >= originalUpdatedAt);
    }

    [Fact]
    public void VehicleModel_Create_ShouldRaiseVehicleModelCreatedEvent()
    {
        var brandId = VehicleBrandId.New();

        var model = VehicleModel.Create(brandId, "  Mobi  ");

        var createdEvent = Assert.Single(model.DomainEvents.OfType<VehicleModelCreated>());
        Assert.Equal(model.Id, createdEvent.VehicleModelId);
        Assert.Equal(brandId, createdEvent.VehicleBrandId);
        Assert.Equal(model.Name.Value, createdEvent.Name);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void VehicleModel_Create_WithEmptyName_ShouldThrowValidationException(string value)
    {
        Assert.Throws<ValidationException>(() => VehicleModel.Create(VehicleBrandId.New(), value));
    }

    [Fact]
    public void VehicleModel_Create_WithNameLongerThanMaxLength_ShouldThrowValidationException()
    {
        var value = new string('A', VehicleModelName.MaxLength + 1);

        Assert.Throws<ValidationException>(() => VehicleModel.Create(VehicleBrandId.New(), value));
    }

    [Fact]
    public void VehicleModel_Create_WithDefaultBrandId_ShouldThrowValidationException()
    {
        Assert.Throws<ValidationException>(() => VehicleModel.Create(default, "Mobi"));
    }

    [Fact]
    public void VehicleModel_Update_ShouldRaiseVehicleModelUpdatedEvent()
    {
        var model = VehicleModel.Create(VehicleBrandId.New(), "Mobi");
        var updatedBrandId = VehicleBrandId.New();
        var originalUpdatedAt = model.UpdatedAt;
        var beforeUpdate = DateTime.UtcNow;

        model.Update(updatedBrandId, "  Pulse  ");

        var updatedEvent = Assert.Single(model.DomainEvents.OfType<VehicleModelUpdated>());
        Assert.Equal(model.Id, updatedEvent.VehicleModelId);
        Assert.Equal(updatedBrandId, updatedEvent.VehicleBrandId);
        Assert.Equal("Pulse", updatedEvent.Name);
        Assert.Equal(updatedBrandId, model.VehicleBrandId);
        Assert.Equal("Pulse", model.Name);
        Assert.True(model.UpdatedAt >= beforeUpdate);
        Assert.True(model.UpdatedAt >= originalUpdatedAt);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void VehicleModel_Update_WithEmptyName_ShouldThrowValidationException(string value)
    {
        var model = VehicleModel.Create(VehicleBrandId.New(), "Mobi");

        Assert.Throws<ValidationException>(() => model.Update(VehicleBrandId.New(), value));
    }

    [Fact]
    public void VehicleModel_Update_WithNameLongerThanMaxLength_ShouldThrowValidationException()
    {
        var model = VehicleModel.Create(VehicleBrandId.New(), "Mobi");
        var value = new string('A', VehicleModelName.MaxLength + 1);

        Assert.Throws<ValidationException>(() => model.Update(VehicleBrandId.New(), value));
    }

    [Fact]
    public void VehicleModel_Update_WithDefaultBrandId_ShouldThrowValidationException()
    {
        var model = VehicleModel.Create(VehicleBrandId.New(), "Mobi");

        Assert.Throws<ValidationException>(() => model.Update(default, "Pulse"));
    }

    [Fact]
    public void VehicleModel_Delete_ShouldRaiseVehicleModelDeletedEvent()
    {
        var model = VehicleModel.Create(VehicleBrandId.New(), "Mobi");
        var originalUpdatedAt = model.UpdatedAt;
        var beforeDelete = DateTime.UtcNow;

        model.Delete();

        var deletedEvent = Assert.Single(model.DomainEvents.OfType<VehicleModelDeleted>());
        Assert.Equal(model.Id, deletedEvent.VehicleModelId);
        Assert.Equal(model.VehicleBrandId, deletedEvent.VehicleBrandId);
        Assert.Equal(model.Name.Value, deletedEvent.Name);
        Assert.True(model.UpdatedAt >= beforeDelete);
        Assert.True(model.UpdatedAt >= originalUpdatedAt);
    }

    [Fact]
    public void VehicleColor_Create_ShouldRaiseVehicleColorCreatedEvent()
    {
        var color = VehicleColor.Create("  Black  ");

        var createdEvent = Assert.Single(color.DomainEvents.OfType<VehicleColorCreated>());
        Assert.Equal(color.Id, createdEvent.VehicleColorId);
        Assert.Equal(color.Name.Value, createdEvent.Name);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void VehicleColor_Create_WithEmptyName_ShouldThrowValidationException(string value)
    {
        Assert.Throws<ValidationException>(() => VehicleColor.Create(value));
    }

    [Fact]
    public void VehicleColor_Create_WithNameLongerThanMaxLength_ShouldThrowValidationException()
    {
        var value = new string('A', VehicleColorName.MaxLength + 1);

        Assert.Throws<ValidationException>(() => VehicleColor.Create(value));
    }

    [Fact]
    public void VehicleColor_Update_ShouldRaiseVehicleColorUpdatedEvent()
    {
        var color = VehicleColor.Create("Black");
        var originalUpdatedAt = color.UpdatedAt;
        var beforeUpdate = DateTime.UtcNow;

        color.Update("  White  ");

        var updatedEvent = Assert.Single(color.DomainEvents.OfType<VehicleColorUpdated>());
        Assert.Equal(color.Id, updatedEvent.VehicleColorId);
        Assert.Equal("White", updatedEvent.Name);
        Assert.Equal("White", color.Name);
        Assert.True(color.UpdatedAt >= beforeUpdate);
        Assert.True(color.UpdatedAt >= originalUpdatedAt);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void VehicleColor_Update_WithEmptyName_ShouldThrowValidationException(string value)
    {
        var color = VehicleColor.Create("Black");

        Assert.Throws<ValidationException>(() => color.Update(value));
    }

    [Fact]
    public void VehicleColor_Update_WithNameLongerThanMaxLength_ShouldThrowValidationException()
    {
        var color = VehicleColor.Create("Black");
        var value = new string('A', VehicleColorName.MaxLength + 1);

        Assert.Throws<ValidationException>(() => color.Update(value));
    }

    [Fact]
    public void VehicleColor_Delete_ShouldRaiseVehicleColorDeletedEvent()
    {
        var color = VehicleColor.Create("Black");
        var originalUpdatedAt = color.UpdatedAt;
        var beforeDelete = DateTime.UtcNow;

        color.Delete();

        var deletedEvent = Assert.Single(color.DomainEvents.OfType<VehicleColorDeleted>());
        Assert.Equal(color.Id, deletedEvent.VehicleColorId);
        Assert.Equal(color.Name.Value, deletedEvent.Name);
        Assert.True(color.UpdatedAt >= beforeDelete);
        Assert.True(color.UpdatedAt >= originalUpdatedAt);
    }

    [Fact]
    public void Vehicle_Create_ShouldRaiseVehicleCreatedEvent()
    {
        var customerId = CustomerId.New();
        const int year = 2024;
        var brandId = VehicleBrandId.New();
        var modelId = VehicleModelId.New();
        var colorId = VehicleColorId.New();
        var licensePlate = LicensePlate.Create("ABC-1234");

        var vehicle = Vehicle.Create(customerId, year, brandId, modelId, colorId, licensePlate);

        var createdEvent = Assert.Single(vehicle.DomainEvents.OfType<VehicleCreated>());
        Assert.Equal(vehicle.Id, createdEvent.VehicleId);
        Assert.Equal(customerId, createdEvent.CustomerId);
        Assert.Equal(year, createdEvent.Year);
        Assert.Equal(brandId, createdEvent.VehicleBrandId);
        Assert.Equal(modelId, createdEvent.VehicleModelId);
        Assert.Equal(colorId, createdEvent.VehicleColorId);
        Assert.Equal(licensePlate.Value, createdEvent.LicensePlate);
        Assert.Equal(customerId, vehicle.CustomerId);
        Assert.Equal(year, vehicle.Year.Value);
    }

    [Fact]
    public void Vehicle_Create_WithDefaultCustomerId_ShouldThrowValidationException()
    {
        Assert.Throws<ValidationException>(() => Vehicle.Create(
            default,
            2024,
            VehicleBrandId.New(),
            VehicleModelId.New(),
            VehicleColorId.New(),
            LicensePlate.Create("ABC1234")));
    }

    [Fact]
    public void Vehicle_Create_WithInvalidYear_ShouldThrowValidationException()
    {
        Assert.Throws<ValidationException>(() => Vehicle.Create(
            CustomerId.New(),
            0,
            VehicleBrandId.New(),
            VehicleModelId.New(),
            VehicleColorId.New(),
            LicensePlate.Create("ABC1234")));
    }

    [Fact]
    public void Vehicle_Create_WithDefaultBrandId_ShouldThrowValidationException()
    {
        Assert.Throws<ValidationException>(() => Vehicle.Create(
            CustomerId.New(),
            2024,
            default,
            VehicleModelId.New(),
            VehicleColorId.New(),
            LicensePlate.Create("ABC1234")));
    }

    [Fact]
    public void Vehicle_Create_WithDefaultModelId_ShouldThrowValidationException()
    {
        Assert.Throws<ValidationException>(() => Vehicle.Create(
            CustomerId.New(),
            2024,
            VehicleBrandId.New(),
            default,
            VehicleColorId.New(),
            LicensePlate.Create("ABC1234")));
    }

    [Fact]
    public void Vehicle_Create_WithDefaultColorId_ShouldThrowValidationException()
    {
        Assert.Throws<ValidationException>(() => Vehicle.Create(
            CustomerId.New(),
            2024,
            VehicleBrandId.New(),
            VehicleModelId.New(),
            default,
            LicensePlate.Create("ABC1234")));
    }

    [Fact]
    public void Vehicle_Create_WithNullLicensePlate_ShouldThrowValidationException()
    {
        Assert.Throws<ValidationException>(() => Vehicle.Create(
            CustomerId.New(),
            2024,
            VehicleBrandId.New(),
            VehicleModelId.New(),
            VehicleColorId.New(),
            null!));
    }

    [Fact]
    public void Vehicle_Update_ShouldRaiseVehicleUpdatedEvent()
    {
        var customerId = CustomerId.New();
        var vehicle = Vehicle.Create(
            customerId,
            2024,
            VehicleBrandId.New(),
            VehicleModelId.New(),
            VehicleColorId.New(),
            LicensePlate.Create("ABC1234"));

        var updatedCustomerId = CustomerId.New();
        const int updatedYear = 2025;
        var updatedBrandId = VehicleBrandId.New();
        var updatedModelId = VehicleModelId.New();
        var updatedColorId = VehicleColorId.New();
        var updatedPlate = LicensePlate.Create("XYZ1A23");
        var originalUpdatedAt = vehicle.UpdatedAt;
        var beforeUpdate = DateTime.UtcNow;

        vehicle.Update(updatedCustomerId, updatedYear, updatedBrandId, updatedModelId, updatedColorId, updatedPlate);

        var updatedEvent = Assert.Single(vehicle.DomainEvents.OfType<VehicleUpdated>());
        Assert.Equal(vehicle.Id, updatedEvent.VehicleId);
        Assert.Equal(updatedCustomerId, updatedEvent.CustomerId);
        Assert.Equal(updatedYear, updatedEvent.Year);
        Assert.Equal(updatedBrandId, updatedEvent.VehicleBrandId);
        Assert.Equal(updatedModelId, updatedEvent.VehicleModelId);
        Assert.Equal(updatedColorId, updatedEvent.VehicleColorId);
        Assert.Equal(updatedPlate.Value, updatedEvent.LicensePlate);
        Assert.Equal(updatedCustomerId, vehicle.CustomerId);
        Assert.Equal(updatedYear, vehicle.Year.Value);
        Assert.Equal(updatedBrandId, vehicle.VehicleBrandId);
        Assert.Equal(updatedModelId, vehicle.VehicleModelId);
        Assert.Equal(updatedColorId, vehicle.VehicleColorId);
        Assert.Equal(updatedPlate, vehicle.LicensePlate);
        Assert.True(vehicle.UpdatedAt >= beforeUpdate);
        Assert.True(vehicle.UpdatedAt >= originalUpdatedAt);
    }

    [Fact]
    public void Vehicle_Update_WithDefaultCustomerId_ShouldThrowValidationException()
    {
        var vehicle = Vehicle.Create(
            CustomerId.New(),
            2024,
            VehicleBrandId.New(),
            VehicleModelId.New(),
            VehicleColorId.New(),
            LicensePlate.Create("ABC1234"));

        Assert.Throws<ValidationException>(() => vehicle.Update(
            default,
            2025,
            VehicleBrandId.New(),
            VehicleModelId.New(),
            VehicleColorId.New(),
            LicensePlate.Create("XYZ1A23")));
    }

    [Fact]
    public void Vehicle_Update_WithInvalidYear_ShouldThrowValidationException()
    {
        var vehicle = Vehicle.Create(
            CustomerId.New(),
            2024,
            VehicleBrandId.New(),
            VehicleModelId.New(),
            VehicleColorId.New(),
            LicensePlate.Create("ABC1234"));

        Assert.Throws<ValidationException>(() => vehicle.Update(
            CustomerId.New(),
            0,
            VehicleBrandId.New(),
            VehicleModelId.New(),
            VehicleColorId.New(),
            LicensePlate.Create("XYZ1A23")));
    }

    [Fact]
    public void Vehicle_Update_WithDefaultBrandId_ShouldThrowValidationException()
    {
        var vehicle = Vehicle.Create(
            CustomerId.New(),
            2024,
            VehicleBrandId.New(),
            VehicleModelId.New(),
            VehicleColorId.New(),
            LicensePlate.Create("ABC1234"));

        Assert.Throws<ValidationException>(() => vehicle.Update(
            CustomerId.New(),
            2025,
            default,
            VehicleModelId.New(),
            VehicleColorId.New(),
            LicensePlate.Create("XYZ1A23")));
    }

    [Fact]
    public void Vehicle_Update_WithDefaultModelId_ShouldThrowValidationException()
    {
        var vehicle = Vehicle.Create(
            CustomerId.New(),
            2024,
            VehicleBrandId.New(),
            VehicleModelId.New(),
            VehicleColorId.New(),
            LicensePlate.Create("ABC1234"));

        Assert.Throws<ValidationException>(() => vehicle.Update(
            CustomerId.New(),
            2025,
            VehicleBrandId.New(),
            default,
            VehicleColorId.New(),
            LicensePlate.Create("XYZ1A23")));
    }

    [Fact]
    public void Vehicle_Update_WithDefaultColorId_ShouldThrowValidationException()
    {
        var vehicle = Vehicle.Create(
            CustomerId.New(),
            2024,
            VehicleBrandId.New(),
            VehicleModelId.New(),
            VehicleColorId.New(),
            LicensePlate.Create("ABC1234"));

        Assert.Throws<ValidationException>(() => vehicle.Update(
            CustomerId.New(),
            2025,
            VehicleBrandId.New(),
            VehicleModelId.New(),
            default,
            LicensePlate.Create("XYZ1A23")));
    }

    [Fact]
    public void Vehicle_Update_WithNullLicensePlate_ShouldThrowValidationException()
    {
        var vehicle = Vehicle.Create(
            CustomerId.New(),
            2024,
            VehicleBrandId.New(),
            VehicleModelId.New(),
            VehicleColorId.New(),
            LicensePlate.Create("ABC1234"));

        Assert.Throws<ValidationException>(() => vehicle.Update(
            CustomerId.New(),
            2025,
            VehicleBrandId.New(),
            VehicleModelId.New(),
            VehicleColorId.New(),
            null!));
    }

    [Fact]
    public void Vehicle_Delete_ShouldRaiseVehicleDeletedEvent()
    {
        var vehicle = Vehicle.Create(
            CustomerId.New(),
            2024,
            VehicleBrandId.New(),
            VehicleModelId.New(),
            VehicleColorId.New(),
            LicensePlate.Create("ABC1234"));

        var originalUpdatedAt = vehicle.UpdatedAt;
        var beforeDelete = DateTime.UtcNow;

        vehicle.Delete();

        var deletedEvent = Assert.Single(vehicle.DomainEvents.OfType<VehicleDeleted>());
        Assert.Equal(vehicle.Id, deletedEvent.VehicleId);
        Assert.Equal(vehicle.LicensePlate.Value, deletedEvent.LicensePlate);
        Assert.True(vehicle.UpdatedAt >= beforeDelete);
        Assert.True(vehicle.UpdatedAt >= originalUpdatedAt);
    }
}
