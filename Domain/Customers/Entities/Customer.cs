using GarageFlow.SharedKernel.Domain.Entities;
using GarageFlow.SharedKernel.Domain.Interfaces;
using GarageFlow.SharedKernel.Domain.ValueObjects;
using GarageFlow.Domain.Customers.Events;
using GarageFlow.Domain.Customers.Enums;
using GarageFlow.Domain.Customers.ValueObjects;
using GarageFlow.SharedKernel.Domain.Exceptions;

namespace GarageFlow.Domain.Customers.Entities;

public sealed class Customer : Entity<CustomerId>, IAggregateRoot
{
    public TaxDocument TaxDocument { get; private set; }
    public FullName FullName { get; private set; }
    public Email Email { get; private set; }
    public PhoneNumber PhoneNumber { get; private set; }
    public CustomerStatus Status { get; private set; }

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
        Status = CustomerStatus.Active;
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

    public void ChangeStatus(CustomerStatus status)
    {
        if (!Enum.IsDefined(status))
        {
            throw new ValidationException($"Customer status '{(int)status}' is invalid.");
        }

        if (Status == status)
        {
            return;
        }

        var previousStatus = Status;
        Status = status;
        UpdatedAt = DateTime.UtcNow;

        RaiseDomainEvent(new CustomerStatusChanged(
            CustomerId: Id,
            PreviousStatus: previousStatus,
            Status: status));
    }
}
