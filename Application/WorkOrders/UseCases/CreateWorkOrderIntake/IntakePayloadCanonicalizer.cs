using System.Security.Cryptography;
using System.Text.Json;
using GarageFlow.Domain.InventoryItems.Enums;
using GarageFlow.Domain.InventoryItems.ValueObjects;
using GarageFlow.Domain.Vehicles.ValueObjects;
using GarageFlow.SharedKernel.Domain.ValueObjects;

namespace GarageFlow.Application.WorkOrders.UseCases.CreateWorkOrderIntake;

internal static class IntakePayloadCanonicalizer
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static string Compute(ValidatedIntakePayload payload)
    {
        var canonicalPayload = new
        {
            customer = new
            {
                taxDocument = payload.TaxDocument.Value,
                fullName = payload.FullName.Value,
                email = payload.Email.Value,
                phoneNumber = payload.PhoneNumber.Value
            },
            vehicle = new
            {
                plate = payload.Plate.Value,
                year = payload.Year.Value,
                brand = payload.Brand.Value,
                model = payload.Model.Value,
                color = payload.Color.Value
            },
            services = payload.Services.Select(service => new
            {
                description = service.Description.Value,
                price = service.Price.Value
            }),
            inventoryItems = payload.InventoryItems.Select(item => new
            {
                name = item.Name.Value,
                description = item.Description.Value,
                type = item.Type.ToString(),
                cost = item.Cost.Value,
                price = item.Price.Value,
                stockQuantity = item.StockQuantity.Value,
                quantity = item.Quantity.Value
            })
        };

        var json = JsonSerializer.SerializeToUtf8Bytes(canonicalPayload, JsonOptions);
        return Convert.ToHexString(SHA256.HashData(json)).ToLowerInvariant();
    }
}

internal sealed record ValidatedIntakePayload(
    TaxDocument TaxDocument,
    FullName FullName,
    Email Email,
    PhoneNumber PhoneNumber,
    LicensePlate Plate,
    VehicleYear Year,
    VehicleBrandName Brand,
    VehicleModelName Model,
    VehicleColorName Color,
    IReadOnlyList<ValidatedIntakeService> Services,
    IReadOnlyList<ValidatedIntakeInventoryItem> InventoryItems);

internal sealed record ValidatedIntakeService(Description Description, Price Price);

internal sealed record ValidatedIntakeInventoryItem(
    InventoryItemName Name,
    Description Description,
    InventoryItemType Type,
    Price Cost,
    Price Price,
    InventoryItemStockQuantity StockQuantity,
    GarageFlow.Domain.WorkOrders.ValueObjects.EstimateItemQuantity Quantity);
