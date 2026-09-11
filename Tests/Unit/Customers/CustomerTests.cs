using GarageFlow.SharedKernel.Domain.Exceptions;
using GarageFlow.SharedKernel.Domain.ValueObjects;
using GarageFlow.Domain.Customers.Entities;
using GarageFlow.Domain.Customers.Enums;
using GarageFlow.Domain.Customers.Events;

namespace GarageFlow.Tests.Unit.Customers;

public class CustomerTests
{
    [Fact]
    public void Create_ShouldInitializeActiveStatus()
    {
        var customer = new GarageFlow.Tests.Shared.Customers.CustomerBuilder().Build();

        Assert.Equal(CustomerStatus.Active, customer.Status);
    }

    [Fact]
    public void ChangeStatus_ShouldUpdateStatusAndRaiseEvent_WhenStatusChanges()
    {
        var customer = new GarageFlow.Tests.Shared.Customers.CustomerBuilder().Build();
        var originalUpdatedAt = customer.UpdatedAt;

        WaitUntilTimeAdvances(originalUpdatedAt);
        customer.ChangeStatus(CustomerStatus.Suspended);

        var statusChanged = Assert.Single(customer.DomainEvents.OfType<CustomerStatusChanged>());
        Assert.Equal(customer.Id, statusChanged.CustomerId);
        Assert.Equal(CustomerStatus.Active, statusChanged.PreviousStatus);
        Assert.Equal(CustomerStatus.Suspended, statusChanged.Status);
        Assert.Equal(CustomerStatus.Suspended, customer.Status);
        Assert.True(customer.UpdatedAt > originalUpdatedAt);
    }

    [Fact]
    public void ChangeStatus_ShouldNotMutateOrRaiseEvent_WhenStatusIsUnchanged()
    {
        var customer = new GarageFlow.Tests.Shared.Customers.CustomerBuilder().Build();
        customer.ClearDomainEvents();
        var originalUpdatedAt = customer.UpdatedAt;

        customer.ChangeStatus(CustomerStatus.Active);

        Assert.Equal(originalUpdatedAt, customer.UpdatedAt);
        Assert.Empty(customer.DomainEvents);
    }

    [Fact]
    public void ChangeStatus_ShouldThrowValidationException_WhenStatusIsUndefined()
    {
        var customer = new GarageFlow.Tests.Shared.Customers.CustomerBuilder().Build();

        var exception = Assert.Throws<ValidationException>(
            () => customer.ChangeStatus((CustomerStatus)999));

        Assert.Equal("Customer status '999' is invalid.", exception.Message);
        Assert.Equal(CustomerStatus.Active, customer.Status);
    }

    [Fact]
    public void Create_ShouldRemoveBrazilCountryCode_WhenPhoneStartsWithPlus55()
    {
        Assert.Equal("11999999999", PhoneNumber.Create("+55 11 99999-9999").Value);
    }

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
