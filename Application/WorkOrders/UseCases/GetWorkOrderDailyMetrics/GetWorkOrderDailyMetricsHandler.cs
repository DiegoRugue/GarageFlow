using GarageFlow.Application.WorkOrders.Ports;
using GarageFlow.SharedKernel.Domain.Exceptions;
using Mediator;

namespace GarageFlow.Application.WorkOrders.UseCases.GetWorkOrderDailyMetrics;

public sealed class GetWorkOrderDailyMetricsHandler(IWorkOrderMetricsQueries workOrderMetricsQueries)
    : IRequestHandler<GetWorkOrderDailyMetricsQuery, GetWorkOrderDailyMetricsResult>
{
    public const string ReportingTimeZoneId = "America/Sao_Paulo";

    private static readonly TimeZoneInfo ReportingTimeZone = TimeZoneInfo.FindSystemTimeZoneById(ReportingTimeZoneId);
    private readonly IWorkOrderMetricsQueries _workOrderMetricsQueries = workOrderMetricsQueries
        ?? throw new ArgumentNullException(nameof(workOrderMetricsQueries));

    public async ValueTask<GetWorkOrderDailyMetricsResult> Handle(GetWorkOrderDailyMetricsQuery request, CancellationToken cancellationToken)
    {
        if (request.Date == DateOnly.MaxValue)
        {
            throw new ValidationException("The reporting date must have a representable following day.");
        }

        var fromUtc = ConvertMidnightToUtc(request.Date);
        var toUtc = ConvertMidnightToUtc(request.Date.AddDays(1));
        var metrics = await _workOrderMetricsQueries.GetDailyAsync(fromUtc, toUtc, cancellationToken);

        return new GetWorkOrderDailyMetricsResult(
            request.Date, ReportingTimeZoneId, fromUtc, toUtc,
            metrics.CreatedCount, metrics.CompletedCount, metrics.AverageDurationSeconds);
    }

    private static DateTime ConvertMidnightToUtc(DateOnly date)
    {
        var midnight = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        if (ReportingTimeZone.IsInvalidTime(midnight) || ReportingTimeZone.IsAmbiguousTime(midnight))
        {
            throw new ValidationException("The reporting date has an invalid or ambiguous midnight in America/Sao_Paulo.");
        }

        return TimeZoneInfo.ConvertTimeToUtc(midnight, ReportingTimeZone);
    }
}
