using GarageFlow.BuildingBlocks.Domain.ValueObjects;
using GarageFlow.Domain.Customers.Entities;

namespace GarageFlow.Tests.Shared.Customers;

public sealed class CustomerBuilder
{
    private string _taxDocument = "529.982.247-25";
    private string _fullName = "John Doe";
    private string _email = "john.doe@example.com";
    private string _phoneNumber = "11987654321";

    public CustomerBuilder WithTaxDocument(string taxDocument)
    {
        _taxDocument = taxDocument;
        return this;
    }

    public CustomerBuilder WithFullName(string fullName)
    {
        _fullName = fullName;
        return this;
    }

    public CustomerBuilder WithEmail(string email)
    {
        _email = email;
        return this;
    }

    public CustomerBuilder WithPhoneNumber(string phoneNumber)
    {
        _phoneNumber = phoneNumber;
        return this;
    }

    public Customer Build()
    {
        return Customer.Create(
            TaxDocument.Create(_taxDocument),
            FullName.Create(_fullName),
            Email.Create(_email),
            PhoneNumber.Create(_phoneNumber));
    }

    public CreateCustomerRequest BuildCreateRequest()
    {
        return new CreateCustomerRequest(
            TaxDocument: _taxDocument,
            FullName: _fullName,
            Email: _email,
            PhoneNumber: _phoneNumber);
    }

    public UpdateCustomerRequest BuildUpdateRequest()
    {
        return new UpdateCustomerRequest(
            FullName: _fullName,
            Email: _email,
            PhoneNumber: _phoneNumber);
    }
}

public sealed record CreateCustomerRequest(
    string TaxDocument,
    string FullName,
    string Email,
    string PhoneNumber);

public sealed record UpdateCustomerRequest(
    string FullName,
    string Email,
    string PhoneNumber);