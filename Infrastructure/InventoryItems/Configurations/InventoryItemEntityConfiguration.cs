using GarageFlow.SharedKernel.Domain.ValueObjects;
using GarageFlow.Domain.InventoryItems.Entities;
using GarageFlow.Domain.InventoryItems.Enums;
using GarageFlow.Domain.InventoryItems.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GarageFlow.Infrastructure.InventoryItems.Configurations;

public sealed class InventoryItemEntityConfiguration : IEntityTypeConfiguration<InventoryItem>
{
    public void Configure(EntityTypeBuilder<InventoryItem> builder)
    {
        builder.ToTable("InventoryItems");

        builder.HasKey(inventoryItem => inventoryItem.Id);

        builder.Property(inventoryItem => inventoryItem.Id)
            .HasConversion(
                inventoryItemId => inventoryItemId.Value,
                value => InventoryItemId.From(value))
            .ValueGeneratedNever();

        builder.Property(inventoryItem => inventoryItem.Name)
            .HasConversion(
                name => name.Value,
                value => InventoryItemName.Create(value))
            .HasMaxLength(InventoryItemName.MaxLength)
            .IsRequired();

        builder.Property(inventoryItem => inventoryItem.Description)
            .HasConversion(
                description => description.Value,
                value => Description.Create(value))
            .HasMaxLength(Description.MaxLength)
            .IsRequired();

        builder.Property(inventoryItem => inventoryItem.Type)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(inventoryItem => inventoryItem.Cost)
            .HasConversion(
                cost => cost.Value,
                value => Price.Create(value))
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(inventoryItem => inventoryItem.Price)
            .HasConversion(
                price => price.Value,
                value => Price.Create(value))
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(inventoryItem => inventoryItem.StockQuantity)
            .HasConversion(
                stockQuantity => stockQuantity.Value,
                value => InventoryItemStockQuantity.Create(value))
            .IsRequired();

        builder.Property(inventoryItem => inventoryItem.CreatedAt)
            .IsRequired();

        builder.Property(inventoryItem => inventoryItem.UpdatedAt)
            .IsRequired();

        builder.Ignore(inventoryItem => inventoryItem.DomainEvents);
    }
}
