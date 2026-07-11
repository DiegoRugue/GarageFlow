using GarageFlow.SharedKernel.Domain.Exceptions;
using GarageFlow.SharedKernel.Domain.ValueObjects;

namespace GarageFlow.Tests.Unit.SharedKernel.ValueObjects;

public class ContactValueObjectsTests
{
    [Theory]
    [InlineData("529.982.247-25", "52998224725")]
    [InlineData("  11144477735  ", "11144477735")]
    public void TaxDocument_Create_ShouldNormalizeCpf(string value, string expected)
    {
        var taxDocument = TaxDocument.Create(value);

        Assert.Equal(expected, taxDocument.Value);
        Assert.Equal(TaxDocumentType.Cpf, taxDocument.DocumentType);
        Assert.Equal(expected, taxDocument.ToString());
        Assert.Equal(expected, (string)taxDocument);
    }

    [Theory]
    [InlineData("11.222.333/0001-81", "11222333000181")]
    [InlineData("  11222333000181  ", "11222333000181")]
    public void TaxDocument_Create_ShouldNormalizeCnpj(string value, string expected)
    {
        var taxDocument = TaxDocument.Create(value);

        Assert.Equal(expected, taxDocument.Value);
        Assert.Equal(TaxDocumentType.Cnpj, taxDocument.DocumentType);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void TaxDocument_Create_ShouldThrowValidationException_WhenEmptyOrWhitespace(string value)
    {
        Assert.Throws<ValidationException>(() => TaxDocument.Create(value));
    }

    [Theory]
    [InlineData("123")]
    [InlineData("123456789012")]
    public void TaxDocument_Create_ShouldThrowValidationException_WhenLengthIsNotCpfOrCnpj(string value)
    {
        Assert.Throws<ValidationException>(() => TaxDocument.Create(value));
    }

    [Theory]
    [InlineData("11111111111")]
    [InlineData("52998224724")]
    public void TaxDocument_Create_ShouldThrowValidationException_WhenCpfIsInvalid(string value)
    {
        Assert.Throws<ValidationException>(() => TaxDocument.Create(value));
    }

    [Theory]
    [InlineData("00000000000000")]
    [InlineData("11222333000180")]
    public void TaxDocument_Create_ShouldThrowValidationException_WhenCnpjIsInvalid(string value)
    {
        Assert.Throws<ValidationException>(() => TaxDocument.Create(value));
    }

    [Fact]
    public void Email_Create_ShouldNormalizeAndReturnValue()
    {
        var email = Email.Create("  customer@example.com  ");

        Assert.Equal("customer@example.com", email.Value);
        Assert.Equal("customer@example.com", email.ToString());
        Assert.Equal("customer@example.com", (string)email);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("invalid-email")]
    public void Email_Create_ShouldThrowValidationException_WhenInvalid(string value)
    {
        Assert.Throws<ValidationException>(() => Email.Create(value));
    }

    [Theory]
    [InlineData("(11) 98765-4321", "11987654321")]
    [InlineData("+11 3333-4444", "1133334444")]
    public void PhoneNumber_Create_ShouldNormalizeAndReturnValue(string value, string expected)
    {
        var phoneNumber = PhoneNumber.Create(value);

        Assert.Equal(expected, phoneNumber.Value);
        Assert.Equal(expected, phoneNumber.ToString());
        Assert.Equal(expected, (string)phoneNumber);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("(11) ABCD-1234")]
    [InlineData("123456789")]
    [InlineData("123456789012")]
    public void PhoneNumber_Create_ShouldThrowValidationException_WhenInvalid(string value)
    {
        Assert.Throws<ValidationException>(() => PhoneNumber.Create(value));
    }

    [Fact]
    public void FullName_Create_ShouldNormalizeAndReturnValue()
    {
        var fullName = FullName.Create("  Maria Silva  ");

        Assert.Equal("Maria Silva", fullName.Value);
        Assert.Equal("Maria Silva", fullName.ToString());
        Assert.Equal("Maria Silva", (string)fullName);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void FullName_Create_ShouldThrowValidationException_WhenEmptyOrWhitespace(string value)
    {
        Assert.Throws<ValidationException>(() => FullName.Create(value));
    }

    [Fact]
    public void FullName_Create_ShouldThrowValidationException_WhenLongerThanMaxLength()
    {
        Assert.Throws<ValidationException>(() => FullName.Create(new string('x', FullName.MaxLength + 1)));
    }
}
