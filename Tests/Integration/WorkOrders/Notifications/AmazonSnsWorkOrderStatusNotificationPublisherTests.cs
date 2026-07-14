using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using GarageFlow.Adapters.Infrastructure.WorkOrders.Notifications;
using GarageFlow.Application.WorkOrders.Integrations;
using Microsoft.Extensions.Options;
using Moq;

namespace GarageFlow.Tests.Integration.WorkOrders.Notifications;

public sealed class AmazonSnsWorkOrderStatusNotificationPublisherTests
{
    private const string TopicArn = "arn:aws:sns:us-east-1:123456789012:garageflow-work-orders";
    private const string SubjectPrefix = "GarageFlow work order ";

    [Fact]
    public async Task PublishAsync_ShouldSendExpectedRequestAndForwardCancellationToken()
    {
        var workOrderId = Guid.Parse("db5e8a33-0d8a-4d75-b177-e570fc748c1d");
        var occurredAt = new DateTime(2026, 7, 12, 14, 25, 36, DateTimeKind.Utc);
        var notification = new WorkOrderStatusChangedIntegrationEvent(
            workOrderId,
            "AwaitingEstimateApproval",
            "Approved",
            occurredAt);
        using var cancellationTokenSource = new CancellationTokenSource();
        PublishRequest? capturedRequest = null;
        CancellationToken capturedCancellationToken = default;
        var client = new Mock<IAmazonSimpleNotificationService>(MockBehavior.Strict);
        client
            .Setup(service => service.PublishAsync(It.IsAny<PublishRequest>(), It.IsAny<CancellationToken>()))
            .Callback<PublishRequest, CancellationToken>((request, cancellationToken) =>
            {
                capturedRequest = request;
                capturedCancellationToken = cancellationToken;
            })
            .ReturnsAsync(new PublishResponse());
        var publisher = CreatePublisher(client.Object);

        await publisher.PublishAsync(notification, cancellationTokenSource.Token);

        Assert.NotNull(capturedRequest);
        Assert.Equal(TopicArn, capturedRequest.TopicArn);
        Assert.Equal("GarageFlow work order Approved", capturedRequest.Subject);
        Assert.True(capturedRequest.Subject.Length < 100);
        Assert.Equal(
            string.Join(
                Environment.NewLine,
                $"Work order: {workOrderId:D}",
                "Previous status: AwaitingEstimateApproval",
                "Current status: Approved",
                $"Occurred at: {occurredAt:O}"),
            capturedRequest.Message);
        Assert.Equal(cancellationTokenSource.Token, capturedCancellationToken);
        client.VerifyAll();
    }

    [Fact]
    public async Task PublishAsync_ShouldPropagateClientException()
    {
        var expectedException = new AmazonSimpleNotificationServiceException("controlled failure");
        var client = new Mock<IAmazonSimpleNotificationService>(MockBehavior.Strict);
        client
            .Setup(service => service.PublishAsync(It.IsAny<PublishRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(expectedException);
        var publisher = CreatePublisher(client.Object);
        var notification = new WorkOrderStatusChangedIntegrationEvent(
            Guid.NewGuid(),
            "Created",
            "InProgress",
            DateTime.UtcNow);

        var actualException = await Assert.ThrowsAsync<AmazonSimpleNotificationServiceException>(
            () => publisher.PublishAsync(notification, CancellationToken.None));

        Assert.Same(expectedException, actualException);
    }

    [Fact]
    public async Task PublishAsync_ShouldRejectNullNotificationBeforeCallingClient()
    {
        var client = new Mock<IAmazonSimpleNotificationService>(MockBehavior.Strict);
        var publisher = CreatePublisher(client.Object);

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => publisher.PublishAsync(null!, CancellationToken.None));

        client.VerifyNoOtherCalls();
    }

    [Theory]
    [MemberData(nameof(InvalidNotifications))]
    public async Task PublishAsync_ShouldRejectInvalidNotificationBeforeCallingClient(
        Guid workOrderId,
        string? previousStatus,
        string? currentStatus,
        DateTime occurredAt)
    {
        var client = new Mock<IAmazonSimpleNotificationService>(MockBehavior.Strict);
        var publisher = CreatePublisher(client.Object);
        var notification = new WorkOrderStatusChangedIntegrationEvent(
            workOrderId,
            previousStatus!,
            currentStatus!,
            occurredAt);

        await Assert.ThrowsAsync<ArgumentException>(
            () => publisher.PublishAsync(notification, CancellationToken.None));

        client.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task PublishAsync_ShouldAcceptSubjectAtMaximumValidLength()
    {
        var currentStatus = new string('A', 99 - SubjectPrefix.Length);
        PublishRequest? capturedRequest = null;
        var client = new Mock<IAmazonSimpleNotificationService>(MockBehavior.Strict);
        client
            .Setup(service => service.PublishAsync(It.IsAny<PublishRequest>(), It.IsAny<CancellationToken>()))
            .Callback<PublishRequest, CancellationToken>((request, _) => capturedRequest = request)
            .ReturnsAsync(new PublishResponse());
        var publisher = CreatePublisher(client.Object);
        var notification = CreateNotification(currentStatus: currentStatus);

        await publisher.PublishAsync(notification, CancellationToken.None);

        Assert.NotNull(capturedRequest);
        Assert.Equal(99, capturedRequest.Subject.Length);
        Assert.Equal($"{SubjectPrefix}{currentStatus}", capturedRequest.Subject);
        client.VerifyAll();
    }

    public static TheoryData<Guid, string?, string?, DateTime> InvalidNotifications()
    {
        var validWorkOrderId = Guid.Parse("db5e8a33-0d8a-4d75-b177-e570fc748c1d");
        var validOccurredAt = new DateTime(2026, 7, 12, 14, 25, 36, DateTimeKind.Utc);

        return new TheoryData<Guid, string?, string?, DateTime>
        {
            { Guid.Empty, "Created", "Approved", validOccurredAt },
            { validWorkOrderId, null, "Approved", validOccurredAt },
            { validWorkOrderId, "", "Approved", validOccurredAt },
            { validWorkOrderId, "   ", "Approved", validOccurredAt },
            { validWorkOrderId, "Created", null, validOccurredAt },
            { validWorkOrderId, "Created", "", validOccurredAt },
            { validWorkOrderId, "Created", "   ", validOccurredAt },
            { validWorkOrderId, "Created", "Approved\rPending", validOccurredAt },
            { validWorkOrderId, "Created", "Approved\nPending", validOccurredAt },
            { validWorkOrderId, "Created", "Approved\u0001Pending", validOccurredAt },
            { validWorkOrderId, "Created", new string('A', 100 - SubjectPrefix.Length), validOccurredAt },
            { validWorkOrderId, "Created", "Approved", new DateTime(2026, 7, 12, 14, 25, 36, DateTimeKind.Local) },
            { validWorkOrderId, "Created", "Approved", new DateTime(2026, 7, 12, 14, 25, 36, DateTimeKind.Unspecified) }
        };
    }

    private static WorkOrderStatusChangedIntegrationEvent CreateNotification(
        Guid? workOrderId = null,
        string? previousStatus = "Created",
        string? currentStatus = "Approved",
        DateTime? occurredAt = null) =>
        new(
            workOrderId ?? Guid.NewGuid(),
            previousStatus!,
            currentStatus!,
            occurredAt ?? new DateTime(2026, 7, 12, 14, 25, 36, DateTimeKind.Utc));

    private static AmazonSnsWorkOrderStatusNotificationPublisher CreatePublisher(
        IAmazonSimpleNotificationService client) =>
        new(
            client,
            Options.Create(new AmazonSnsStatusNotificationOptions
            {
                Region = AmazonSnsStatusNotificationOptions.RequiredRegion,
                TopicArn = TopicArn
            }));
}
