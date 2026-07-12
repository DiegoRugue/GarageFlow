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
    [InlineData("us-east-2", "arn:aws:sns:us-east-1:123456789012:garageflow-work-orders")]
    [InlineData("US-EAST-1", "arn:aws:sns:us-east-1:123456789012:garageflow-work-orders")]
    [InlineData("us-east-1", "")]
    [InlineData("us-east-1", "arn:aws:sns:us-east-1:123456789012:placeholder")]
    [InlineData("us-east-1", "arn:aws:sqs:us-east-1:123456789012:garageflow-work-orders")]
    [InlineData("us-east-1", "arn:aws:sns:eu-west-1:123456789012:garageflow-work-orders")]
    [InlineData("us-east-1", "arn:aws:sns:us-east-1:not-an-account:garageflow-work-orders")]
    [InlineData("us-east-1", "arn:aws:sns:us-east-1:123456789012:garageflow/work-orders")]
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
}
