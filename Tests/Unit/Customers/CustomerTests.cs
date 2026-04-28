using GarageFlow.BuildingBlocks.Domain.Exceptions;
using GarageFlow.BuildingBlocks.Domain.ValueObjects;
using GarageFlow.Domain.Customers.Entities;
using GarageFlow.Domain.Customers.Events;

namespace GarageFlow.Tests.Unit.Customers;

public class CustomerTests
{
    [Fact]
    public void Create_ShouldRaiseCustomerCreatedEvent()
    {
        var taxDocument = TaxDocument.Create("11144477735");
        var fullName = FullName.Create("John Doe");
        var email = Email.Create("john.doe@example.com");
        var phoneNumber = PhoneNumber.Create("11987654321");

        var customer = Customer.Create(taxDocument, fullName, email, phoneNumber);

        var createdEvent = Assert.Single(customer.DomainEvents.OfType<CustomerCreated>());
        Assert.Equal(customer.Id, createdEvent.CustomerId);
        Assert.Equal(taxDocument.Value, createdEvent.TaxDocument);
        Assert.Equal(fullName.Value, createdEvent.FullName);
        Assert.Equal(email.Value, createdEvent.Email);
        Assert.Equal(phoneNumber.Value, createdEvent.PhoneNumber);
    }

    [Fact]
    public void Update_ShouldRaiseCustomerUpdatedEvent()
    {
        var customer = Customer.Create(
            TaxDocument.Create("11144477735"),
            FullName.Create("John Doe"),
            Email.Create("john.doe@example.com"),
            PhoneNumber.Create("11987654321"));
        var originalUpdatedAt = customer.UpdatedAt;

        var updatedName = FullName.Create("John Updated");
        var updatedEmail = Email.Create("john.updated@example.com");
        var updatedPhone = PhoneNumber.Create("11912345678");

        WaitUntilTimeAdvances(originalUpdatedAt);
        customer.Update(updatedName, updatedEmail, updatedPhone);

        var updatedEvent = Assert.Single(customer.DomainEvents.OfType<CustomerUpdated>());
        Assert.Equal(customer.Id, updatedEvent.CustomerId);
        Assert.Equal(updatedName.Value, updatedEvent.FullName);
        Assert.Equal(updatedEmail.Value, updatedEvent.Email);
        Assert.Equal(updatedPhone.Value, updatedEvent.PhoneNumber);
        Assert.Equal(updatedName, customer.FullName);
        Assert.Equal(updatedEmail, customer.Email);
        Assert.Equal(updatedPhone, customer.PhoneNumber);
        Assert.True(customer.UpdatedAt > originalUpdatedAt);
    }

    [Fact]
    public void Delete_ShouldRaiseCustomerDeletedEvent()
    {
        var customer = Customer.Create(
            TaxDocument.Create("11144477735"),
            FullName.Create("John Doe"),
            Email.Create("john.doe@example.com"),
            PhoneNumber.Create("11987654321"));
        var originalUpdatedAt = customer.UpdatedAt;

        WaitUntilTimeAdvances(originalUpdatedAt);
        customer.Delete();

        var deletedEvent = Assert.Single(customer.DomainEvents.OfType<CustomerDeleted>());
        Assert.Equal(customer.Id, deletedEvent.CustomerId);
        Assert.Equal(customer.TaxDocument.Value, deletedEvent.TaxDocument);
        Assert.True(customer.UpdatedAt > originalUpdatedAt);
    }

    [Fact]
    public void Create_WithRepeatedDigitCnpj_ShouldThrowValidationException()
    {
        var exception = Assert.Throws<ValidationException>(() => TaxDocument.Create("00.000.000/0000-00"));

        Assert.Equal("Invalid CNPJ.", exception.Message);
    }

    private static void WaitUntilTimeAdvances(DateTime referenceUtc)
    {
        while (DateTime.UtcNow <= referenceUtc)
        {
            Thread.SpinWait(50);
        }
    }
}
