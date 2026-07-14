using GarageFlow.Adapters.Infrastructure.WorkOrders.Notifications;

namespace GarageFlow.Tests.Integration.WorkOrders.Notifications;

public sealed class AmazonSnsStatusNotificationOptionsValidatorTests
{
    [Fact]
    public void Validate_ShouldSucceedForRequiredRegionAndSnsTopicArn()
    {
        var options = new AmazonSnsStatusNotificationOptions
        {
            Region = AmazonSnsStatusNotificationOptions.RequiredRegion,
            TopicArn = "arn:aws:sns:us-east-1:123456789012:garageflow-work-orders"
        };

        var result = new AmazonSnsStatusNotificationOptionsValidator().Validate(null, options);

        Assert.True(result.Succeeded);
    }

    [Theory]
    [InlineData("garageflow_work-orders-123")]
    [InlineData("A")]
    public void Validate_ShouldSucceedForStandardTopicNameCharacters(string topicName)
    {
        var options = new AmazonSnsStatusNotificationOptions
        {
            Region = AmazonSnsStatusNotificationOptions.RequiredRegion,
            TopicArn = $"arn:aws:sns:us-east-1:123456789012:{topicName}"
        };

        var result = new AmazonSnsStatusNotificationOptionsValidator().Validate(null, options);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_ShouldEnforceTopicNameLengthBoundary()
    {
        var validator = new AmazonSnsStatusNotificationOptionsValidator();
        var valid = validator.Validate(null, CreateOptions(new string('a', 256)));
        var invalid = validator.Validate(null, CreateOptions(new string('a', 257)));

        Assert.True(valid.Succeeded);
        Assert.True(invalid.Failed);
    }

    [Theory]
    [InlineData("us-east-2", "arn:aws:sns:us-east-1:123456789012:garageflow-work-orders")]
    [InlineData("US-EAST-1", "arn:aws:sns:us-east-1:123456789012:garageflow-work-orders")]
    [InlineData("us-east-1", "")]
    [InlineData("us-east-1", "arn:aws:sns:us-east-1:123456789012:placeholder")]
    [InlineData("us-east-1", "arn:aws:sqs:us-east-1:123456789012:garageflow-work-orders")]
    [InlineData("us-east-1", "arn:aws:sns:eu-west-1:123456789012:garageflow-work-orders")]
    [InlineData("us-east-1", "arn:aws:sns:us-east-1:not-an-account:garageflow-work-orders")]
    [InlineData("us-east-1", "arn:aws:sns:us-east-1:123456789012:garageflow/work-orders")]
    [InlineData("us-east-1", "arn:aws:sns:us-east-1:123456789012:garageflow.work-orders")]
    [InlineData("us-east-1", "arn:aws:sns:us-east-1:123456789012:garageflow-work-orders.fifo")]
    [InlineData("us-east-1", "arn:aws:sns:us-east-1:123456789012:PLACEHOLDER")]
    [InlineData("us-east-1", "arn:aws:sns:us-east-1:123456789012:REPLACE_ME")]
    [InlineData("us-east-1", "arn:aws:sns:us-east-1:123456789012:CHANGE_ME")]
    [InlineData("us-east-1", "arn:aws:sns:us-east-1:123456789012:__SET_ME__")]
    [InlineData("us-east-1", "arn:aws:sns:us-east-1:123456789012:CONFIGURE_ME")]
    [InlineData("us-east-1", "arn:aws:sns:us-east-1:123456789012:TODO")]
    [InlineData("us-east-1", "arn:aws:sns:us-east-1:123456789012:garagêflow")]
    public void Validate_ShouldFailForInvalidSettings(string region, string topicArn)
    {
        var options = new AmazonSnsStatusNotificationOptions
        {
            Region = region,
            TopicArn = topicArn
        };

        var result = new AmazonSnsStatusNotificationOptionsValidator().Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains(AmazonSnsStatusNotificationOptions.SectionName, result.FailureMessage);
    }

    private static AmazonSnsStatusNotificationOptions CreateOptions(string topicName) =>
        new()
        {
            Region = AmazonSnsStatusNotificationOptions.RequiredRegion,
            TopicArn = $"arn:aws:sns:us-east-1:123456789012:{topicName}"
        };
}
