using GarageFlow.Application.InventoryItems.Common;
using GarageFlow.Application.WorkOrders.UseCases.CreateWorkOrderIntake;
using GarageFlow.Domain.InventoryItems.ValueObjects;
using GarageFlow.Domain.Vehicles.ValueObjects;
using GarageFlow.Domain.WorkOrders.ValueObjects;
using GarageFlow.SharedKernel.Domain.ValueObjects;

namespace GarageFlow.Tests.Unit.WorkOrders;

public sealed class IntakePayloadCanonicalizerTests
{
    private const string LegacyMinimalScaleHash =
        "dc8e91228d60101a67671c74e4a162ffb0997a13826e94fe28180c0dea165499";

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
    public void Compute_ProducesEquivalentHash_ForEquivalentPriceScales()
    {
        (decimal MinimalScale, decimal RedundantScale)[] equivalentPriceScales =
        [
            (120m, 120.0m),
            (120m, 120.00m),
            (120m, 120.00000000000000000000000000m),
            (40.5m, 40.50m),
            (0m, 0.00m)
        ];

        foreach (var (minimalScale, redundantScale) in equivalentPriceScales)
        {
            var canonical = CreatePayload(
                servicePrice: minimalScale,
                inventoryCost: minimalScale,
                inventoryPrice: minimalScale);
            var equivalent = CreatePayload(
                servicePrice: redundantScale,
                inventoryCost: redundantScale,
                inventoryPrice: redundantScale);

            Assert.Equal(
                IntakePayloadCanonicalizer.Compute(canonical),
                IntakePayloadCanonicalizer.Compute(equivalent));
        }
    }

    [Fact]
    public void Compute_PreservesLegacyHash_ForMinimalScalePrices()
    {
        Assert.Equal(LegacyMinimalScaleHash, IntakePayloadCanonicalizer.Compute(CreatePayload()));
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
    public void Compute_IsSensitiveToEachPriceChange()
    {
        var original = CreatePayload();
        var originalHash = IntakePayloadCanonicalizer.Compute(original);

        Assert.NotEqual(originalHash, IntakePayloadCanonicalizer.Compute(CreatePayload(servicePrice: 121m)));
        Assert.NotEqual(originalHash, IntakePayloadCanonicalizer.Compute(CreatePayload(inventoryCost: 21m)));
        Assert.NotEqual(originalHash, IntakePayloadCanonicalizer.Compute(CreatePayload(inventoryPrice: 41m)));
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
        decimal servicePrice = 120m,
        decimal inventoryCost = 20m,
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
                .Select(description => new ValidatedIntakeService(
                    Description.Create(description),
                    Price.Create(servicePrice)))
                .ToList(),
            [
                new ValidatedIntakeInventoryItem(
                    InventoryItemName.Create("Oil filter"),
                    Description.Create("Premium filter"),
                    InventoryItemTypeParser.Parse(inventoryType),
                    Price.Create(inventoryCost),
                    Price.Create(inventoryPrice),
                    InventoryItemStockQuantity.Create(10),
                    EstimateItemQuantity.Create(2))
            ]);
    }
}
