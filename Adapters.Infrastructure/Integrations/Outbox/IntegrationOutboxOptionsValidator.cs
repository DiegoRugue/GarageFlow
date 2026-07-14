using Microsoft.Extensions.Options;

namespace GarageFlow.Adapters.Infrastructure.Integrations.Outbox;

public sealed class IntegrationOutboxOptionsValidator : IValidateOptions<IntegrationOutboxOptions>
{
    public ValidateOptionsResult Validate(string? name, IntegrationOutboxOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return options.IsValid()
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(
                "Integrations:Outbox requires positive batch, polling, lease, and retry values, " +
                "and MaxRetryDelaySeconds must be greater than or equal to InitialRetryDelaySeconds.");
    }
}
