using GarageFlow.SharedKernel.Domain.ValueObjects;
using GarageFlow.Domain.Customers.Entities;
using GarageFlow.Domain.Customers.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GarageFlow.Infrastructure.Customers.Configurations;

public sealed class CustomerEntityConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.ToTable("Customers");

        builder.HasKey(customer => customer.Id);

        builder.Property(customer => customer.Id)
            .HasConversion(
                customerId => customerId.Value,
                value => CustomerId.From(value))
            .ValueGeneratedNever();

        builder.Property(customer => customer.TaxDocument)
            .HasConversion(
                taxDocument => taxDocument.Value,
                value => TaxDocument.Create(value))
            .HasMaxLength(14)
            .IsRequired();

        builder.HasIndex(customer => customer.TaxDocument)
            .IsUnique();

        builder.Property(customer => customer.FullName)
            .HasConversion(
                fullName => fullName.Value,
                value => FullName.Create(value))
            .HasMaxLength(FullName.MaxLength)
            .IsRequired();

        builder.Property(customer => customer.Email)
            .HasConversion(
                email => email.Value,
                value => Email.Create(value))
            .HasMaxLength(254)
            .IsRequired();

        builder.Property(customer => customer.PhoneNumber)
            .HasConversion(
                phoneNumber => phoneNumber.Value,
                value => PhoneNumber.Create(value))
            .HasMaxLength(11)
            .IsRequired();

        builder.Property(customer => customer.CreatedAt)
            .IsRequired();

        builder.Property(customer => customer.UpdatedAt)
            .IsRequired();

        builder.Ignore(customer => customer.DomainEvents);
    }
}
