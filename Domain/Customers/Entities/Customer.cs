using GarageFlow.BuildingBlocks.Domain.Entities;
using GarageFlow.BuildingBlocks.Domain.Interfaces;
using GarageFlow.BuildingBlocks.Domain.ValueObjects;
using GarageFlow.Domain.Customers.Events;
using GarageFlow.Domain.Customers.ValueObjects;

namespace GarageFlow.Domain.Customers.Entities;

public sealed class Customer : Entity<CustomerId>, IAggregateRoot
{
    public TaxDocument TaxDocument { get; private set; }
    public FullName FullName { get; private set; }
    public Email Email { get; private set; }
    public PhoneNumber PhoneNumber { get; private set; }

    private Customer(
        CustomerId id,
        TaxDocument taxDocument,
        FullName fullName,
        Email email,
        PhoneNumber phoneNumber) : base(id)
    {
        TaxDocument = taxDocument;
        FullName = fullName;
        Email = email;
        PhoneNumber = phoneNumber;
    }

    public static Customer Create(
        TaxDocument taxDocument,
        FullName fullName,
        Email email,
        PhoneNumber phoneNumber)
    {
        var id = CustomerId.New();
        var customer = new Customer(id, taxDocument, fullName, email, phoneNumber);

        customer.RaiseDomainEvent(new CustomerCreated(
            CustomerId: id,
            TaxDocument: taxDocument.Value,
            FullName: fullName.Value,
            Email: email.Value,
            PhoneNumber: phoneNumber.Value,
            CreatedAt: customer.CreatedAt));

        return customer;
    }

    public void Update(FullName fullName, Email email, PhoneNumber phoneNumber)
    {
        FullName = fullName;
        Email = email;
        PhoneNumber = phoneNumber;
        UpdatedAt = DateTime.UtcNow;

        RaiseDomainEvent(new CustomerUpdated(
            CustomerId: Id,
            FullName: fullName.Value,
            Email: email.Value,
            PhoneNumber: phoneNumber.Value));
    }

    public void Delete()
    {
        UpdatedAt = DateTime.UtcNow;

        RaiseDomainEvent(new CustomerDeleted(
            CustomerId: Id,
            TaxDocument: TaxDocument.Value));
    }
}
