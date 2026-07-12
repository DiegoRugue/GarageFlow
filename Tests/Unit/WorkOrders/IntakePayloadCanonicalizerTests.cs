using GarageFlow.Application.InventoryItems.Common;
using GarageFlow.Application.WorkOrders.UseCases.CreateWorkOrderIntake;
using GarageFlow.Domain.InventoryItems.ValueObjects;
using GarageFlow.Domain.Vehicles.ValueObjects;
using GarageFlow.Domain.WorkOrders.ValueObjects;
using GarageFlow.SharedKernel.Domain.ValueObjects;

namespace GarageFlow.Tests.Unit.WorkOrders;

public sealed class IntakePayloadCanonicalizerTests
{
    [Fact]
    public void Compute_CanonicalContractExcludesRequestIdentifier()
    {
        var payload = CreatePayload();
        var computeMethod = typeof(IntakePayloadCanonicalizer).GetMethod(nameof(IntakePayloadCanonicalizer.Compute));

        Assert.NotNull(computeMethod);
        var parameter = Assert.Single(computeMethod.GetParameters());
        Assert.Equal(typeof(ValidatedIntakePayload), parameter.ParameterType);
        Assert.DoesNotContain(
            typeof(ValidatedIntakePayload).GetProperties(),
            property => string.Equals(property.Name, "RequestId", StringComparison.Ordinal));
        Assert.NotEmpty(IntakePayloadCanonicalizer.Compute(payload));
    }

    [Fact]
    public void Compute_ProducesEquivalentHash_AfterValueNormalizationAndCanonicalEnumParsing()
    {
        var canonical = CreatePayload();
        var normalized = CreatePayload(
            taxDocument: "529.982.247-25",
            fullName: "  Ana Silva  ",
            phoneNumber: "+55 (11) 91234-5678",
            plate: "abc-1d23",
            brand: "  Honda  ",
            model: "  Civic  ",
            color: "  Black  ",
            inventoryType: "  pArT  ");

        Assert.Equal(
            IntakePayloadCanonicalizer.Compute(canonical),
            IntakePayloadCanonicalizer.Compute(normalized));
    }

    [Fact]
    public void Compute_IsSensitiveToCollectionOrder()
    {
        var original = CreatePayload(serviceDescriptions: ["Oil change", "Alignment"]);
        var reordered = CreatePayload(serviceDescriptions: ["Alignment", "Oil change"]);

        Assert.NotEqual(
            IntakePayloadCanonicalizer.Compute(original),
            IntakePayloadCanonicalizer.Compute(reordered));
    }

    [Fact]
    public void Compute_IsSensitiveToPayloadChanges()
    {
        var original = CreatePayload();
        var changed = CreatePayload(inventoryPrice: 41m);

        Assert.NotEqual(
            IntakePayloadCanonicalizer.Compute(original),
            IntakePayloadCanonicalizer.Compute(changed));
    }

    private static ValidatedIntakePayload CreatePayload(
        string taxDocument = "52998224725",
        string fullName = "Ana Silva",
        string phoneNumber = "11912345678",
        string plate = "ABC1D23",
        string brand = "Honda",
        string model = "Civic",
        string color = "Black",
        string inventoryType = "Part",
        decimal inventoryPrice = 40m,
        IReadOnlyList<string>? serviceDescriptions = null)
    {
        serviceDescriptions ??= ["Oil change"];

        return new ValidatedIntakePayload(
            TaxDocument.Create(taxDocument),
            FullName.Create(fullName),
            Email.Create("ana@example.com"),
            PhoneNumber.Create(phoneNumber),
            LicensePlate.Create(plate),
            VehicleYear.Create(2025),
            VehicleBrandName.Create(brand),
            VehicleModelName.Create(model),
            VehicleColorName.Create(color),
            serviceDescriptions
                .Select(description => new ValidatedIntakeService(Description.Create(description), Price.Create(120m)))
                .ToList(),
            [
                new ValidatedIntakeInventoryItem(
                    InventoryItemName.Create("Oil filter"),
                    Description.Create("Premium filter"),
                    InventoryItemTypeParser.Parse(inventoryType),
                    Price.Create(20m),
                    Price.Create(inventoryPrice),
                    InventoryItemStockQuantity.Create(10),
                    EstimateItemQuantity.Create(2))
            ]);
    }
}
