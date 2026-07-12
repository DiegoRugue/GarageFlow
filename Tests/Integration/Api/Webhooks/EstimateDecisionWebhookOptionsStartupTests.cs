using GarageFlow.Tests.Integration.Support.Factories;
using GarageFlow.Tests.Integration.Support.Fixtures;
using Microsoft.Extensions.Options;

namespace GarageFlow.Tests.Integration.Api.Webhooks;

[Collection(WebApplicationFactoryStartupTestGroup.Name)]
public sealed class EstimateDecisionWebhookOptionsStartupTests
{
    [Fact]
    public void IntegrationTestsEnvironment_ShouldRejectInvalidWebhookSecretAtStartup()
    {
        using var factory = new GarageFlowWebApplicationFactory(
            $"garageflow-options-tests-{Guid.NewGuid():N}",
            estimateDecisionWebhookSecret: "too-short");

        var exception = Assert.Throws<OptionsValidationException>(() => factory.CreateClient());

        Assert.Contains(
            "outside Development and Integration.",
            exception.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void IntegrationTestsEnvironment_ShouldRejectWhitespaceWebhookSecretAtStartup()
    {
        using var factory = new GarageFlowWebApplicationFactory(
            $"garageflow-options-tests-{Guid.NewGuid():N}",
            estimateDecisionWebhookSecret: new string(' ', 32));

        var exception = Assert.Throws<OptionsValidationException>(() => factory.CreateClient());

        Assert.Contains(
            "outside Development and Integration.",
            exception.Message,
            StringComparison.Ordinal);
    }
}
