# ADR 0003 — Daily work-order snapshots from persisted execution timestamps

## Status

Accepted for implementation. Production export and dashboard queries require separate acceptance after deployment.

## Context

The workshop needs daily work-order volume and elapsed time from approved execution to completion. WorkOrder already persists CreatedAt, StartedAt and CompletedAt. StartedAt is assigned when the estimate is approved and the order enters InProgress; CompletedAt is assigned when all services on the approved estimate are completed. Delivery is a later transition that preserves these timestamps.

The existing AverageServiceTime query measures individual service lines. Changing that contract would change existing clients' meaning and weight orders with multiple services differently. Incrementing business counters independently in each API process would also make restarts, replica count and repeated export affect the apparent totals.

## Decision

Compute daily summaries from the database through a side-effect-free Application query and an infrastructure query port implementation. The Application owns the reporting-day contract and UTC boundaries; EF Core/Npgsql executes aggregates over WorkOrder without joining service lines. No new public endpoint, schema migration, status history or change to the state machine is required.

- Creation volume counts orders with CreatedAt in the selected day, regardless of their later state.
- Completion count and mean select Completed or Delivered orders by CompletedAt in that day, requiring both timestamps and CompletedAt greater than or equal to StartedAt.
- Elapsed time is CompletedAt minus StartedAt, in seconds. Each eligible order contributes once. The mean is absent when there are no eligible orders; equal real timestamps can legitimately produce zero.
- Open, cancelled, incomplete or chronologically invalid orders do not contribute to completion count or mean.
- Reporting days use America/Sao_Paulo and half-open UTC intervals: start inclusive, next-day start exclusive. Creation and completion widgets represent different event dates. Dates whose midnight boundaries are invalid or ambiguous in that time zone are rejected explicitly instead of selecting an offset silently; the current dashboard reads recent reporting dates.

This is elapsed approved-execution time, including waiting that occurs after approval. It excludes earlier diagnosis, rejected estimates, waiting for approval and waiting to collect a completed vehicle. It is not a sum of mechanic labor hours or a measurement of diagnosis duration.

## Publication and replica behavior

With Observability.Enabled enabled, a Host background service reads today and the previous six reporting days at startup, waiting five minutes after each refresh before starting the next. Refresh work has a 30-second cancellation budget, runs in its own scope, and never executes database queries from metric callbacks or HTTP request handling. A successful refresh replaces an immutable snapshot. Snapshots older than ten minutes stop contributing observable measurements. A failed refresh does not advance the successful-refresh timestamp; future cycles retry without exposing exception details.

The Host owns meter GarageFlow.WorkOrders and publishes these gauges through the existing private OTLP collector:

| Instrument | Value |
|---|---|
| garageflow.work_orders.created | Orders created on the reporting date |
| garageflow.work_orders.completed | Eligible orders completed on the reporting date |
| garageflow.work_orders.duration.mean | Mean CompletedAt - StartedAt in seconds; absent without samples |
| garageflow.work_orders.snapshot.timestamp | Start of the successful refresh, Unix seconds |

Metric attributes are work_orders.date (yyyy-MM-dd) and work_orders.timezone (America/Sao_Paulo), alongside the existing service, environment and instance resource attributes. Order/customer identifiers, CPF, tokens and SQL are not exported. No New Relic key is added to the API; the collector continues to own ingestion credentials and TLS to New Relic.

Every replica may publish a daily summary. Dashboards select latest values per reporting date with service/environment filters. They must not sum the snapshots across replicas or export intervals. The business date attribute drives the chart categories; an export-time TIMESERIES would describe repeated snapshots rather than daily creation volume. The display includes completion sample count and refresh time. The current day is partial; summaries are eventually refreshed, not a transactional live feed.

The refresh start instant also determines the seven reporting dates. Ranking date facets by their maximum snapshot timestamp before limiting to seven prevents a preceding-day window from retaining an eighth date around midnight, even when replicas finish their refreshes out of order. A timestamp is published only when the whole refresh succeeds; it is not an assertion of transactional consistency across the database reads.

## Consequences and boundaries

Reading persisted rows lets a new process recover recent business totals after an Academy session interruption without replaying domain events or introducing leader election. The tradeoff is duplicated bounded polling and ingestion across replicas. Database aggregate latency and cardinality should be measured before increasing the lookback or refresh frequency.

The dashboard's recent telemetry window is separate from its seven business dates. Missing data or stale refresh time must not be presented as zero activity or healthy uptime. The absence of a mean must not be converted to zero; use the latest completion count to distinguish a stale previous mean from a current summary with no eligible completions.

The pre-existing per-service query remains unchanged. Diagnosis duration, integration failures, work-order failure alerts and uptime have separate acceptance; these two business indicators do not fulfill all observability requirements by themselves. Lambda instrumentation and automatic redistribution of Kubernetes pods remain outside this change.

## Verification and rollout

Use deterministic Unit tests for reporting-day boundaries and PostgreSQL tests for database aggregation, multiple services on one order, state/date eligibility, empty results and true zero durations. Verify publication, freshness, cancellation and safe attributes with controlled time and actual metric export. Required build, Unit, Integration, Docker/PostgreSQL E2E and full-solution checks apply.

The collector already accepts application metrics. Deploy a new reviewed application commit through the normal protected workflow with Observability.Enabled enabled. Render and import the business dashboard from the platform repository, then compare a known order's database duration and daily totals against the displayed summaries. A valid JSON file or passing local exporter test alone does not establish New Relic production acceptance.

## Sources

- [Npgsql query translations](https://www.npgsql.org/efcore/mapping/translations.html)
- [.NET metric instruments](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/metrics-instrumentation)
- [OpenTelemetry metric mapping in New Relic](https://docs.newrelic.com/docs/opentelemetry/best-practices/opentelemetry-best-practices-metrics/)
- [Querying dimensional metrics](https://docs.newrelic.com/docs/data-apis/understand-data/metric-data/query-metric-data-type/)
