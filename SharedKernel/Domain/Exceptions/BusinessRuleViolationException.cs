namespace GarageFlow.SharedKernel.Domain.Exceptions;

public sealed class BusinessRuleViolationException : Exception
{
    public string Rule { get; }

    public BusinessRuleViolationException(string message)
        : base(message)
    {
        Rule = string.Empty;
    }

    public BusinessRuleViolationException(string rule, string message)
        : base(message)
    {
        Rule = rule;
    }

    public BusinessRuleViolationException(string message, Exception innerException)
        : base(message, innerException)
    {
        Rule = string.Empty;
    }
}
