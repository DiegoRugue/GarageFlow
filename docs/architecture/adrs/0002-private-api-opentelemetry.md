# ADR 0002 — Opt-in API OpenTelemetry through a private collector

## Status

Accepted for implementation. Production ingestion is verified separately after the platform collector and application deployment are enabled.

## Context

The API needs request latency, error and runtime visibility in New Relic without introducing telemetry dependencies in Domain or Application, exposing customer information, or requiring a New Relic key in the API workload.

## Decision

The Host owns OpenTelemetry .NET SDK, ASP.NET Core and HttpClient tracing, built-in HTTP duration metrics and runtime metrics. Packages are pinned to 1.18.0. `Observability:Enabled` defaults to false. `Observability:OtlpEndpoint` defaults to `http://garageflow-otel.newrelic.svc.cluster.local:4318`; the exporter sends HTTP/protobuf to `/v1/traces`, `/v1/metrics` and `/v1/logs`. An enabled endpoint must be an HTTP(S) base URL without credentials, extra path, query or fragment.

All signals use `service.name=garageflow-api`, one process-scoped `service.instance.id`, and `deployment.environment.name` from `Observability:Environment` (Host environment fallback locally). The Phase 3 manifest renderer always supplies the protected deployment environment explicitly. The platform enriches Kubernetes identity and owns the New Relic key and TLS export to New Relic. No public receiver is introduced.

When enabled, JSON console and OTLP logs admit only the Host request-completion category, whose fixed schema includes normalized method, route template (or `unmatched`), response status, duration, trace ID and span ID. Arbitrary framework logs, EF SQL, exception messages and logging scopes are excluded. Kubernetes collection must exclude API stdout to prevent duplicate ingestion. Normal logging remains unchanged while observability is disabled.

Trace export permits only method, status, route template, protocol version and URL scheme; error spans use the fixed `error.type=request_failed`. Raw URLs, paths, query strings, user-agent, host, request/response bodies, headers, passwords, JWTs, CPF, SQL, parameters, baggage and tracestate are not exported. Span names use route templates or a fixed client name; exception recording is disabled, and exception status descriptions are removed. HTTP metrics retain only method/status/route/error dimensions; other HTTP instruments are dropped. Runtime measurements contain framework-defined dimensions.

Traces and logs use asynchronous batches with a 2,048-item queue, 512-item maximum batch, five-second schedule and two-second export timeout. Metrics export every 30 seconds with a two-second timeout. Export failure is isolated from HTTP handling; overload may drop telemetry. The JSON console queue also holds at most 2,048 records and drops new records when full, avoiding stdout backpressure on request handling. No request waits for export. Root requests are fully sampled, while remote parent sampling is respected. Export cost should be measured before changing sampling or HPA settings.

## Consequences and boundaries

The API produces technical request and runtime telemetry. Database/Npgsql tracing is deliberately excluded because SQL and exception-event privacy have not been established. SNS, outbox and business-specific spans are not added. Lambda telemetry, work-order duration semantics, historical backfill and business dashboards remain outside this change.

The restricted logging policy sacrifices framework exception stacks and ad hoc diagnostic messages. New operational log schemas must receive privacy review before being admitted. Trace IDs still correlate requests with safe logs. Missing telemetry is not evidence of uptime. Deployment and successful New Relic ingestion must be validated independently of local tests.

## Activation and rollback

Deploy and verify the private collector first. Set the protected application environment variable `OBSERVABILITY_ENABLED=true` before merging the application change, then let the existing main/develop workflow deploy that new commit. Merge/deploy the platform first and validate its collector before the application merge; commit-SHA image tags are immutable, so activation must use a new commit rather than rerunning an already-published SHA. The renderer supplies `Observability__Enabled`, `Observability__OtlpEndpoint` and `Observability__Environment` in the ConfigMap. Leave the flag unset/false until the collector is ready. Set false and deploy to disable export. New Relic credentials belong only to the platform collector, never this application configuration.

## Verification

Integration tests exercise a real HTTP request, correlation between span/log IDs, route metrics, sensitive header/path/query/body/tracestate handling, handled HTTP 500, invalid options, disabled mode and unavailable collectors. A local HTTP/protobuf receiver verifies all three actual OTLP signals and consistent resources. Deployment script tests verify opt-in values and the private receiver contract. Full build, unit, integration and Docker/PostgreSQL E2E suites remain required.

## Sources

- [OpenTelemetry .NET 1.18.0](https://github.com/open-telemetry/opentelemetry-dotnet/tree/core-1.18.0)
- [ASP.NET Core instrumentation](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/tree/main/src/OpenTelemetry.Instrumentation.AspNetCore)
- [OTLP exporter security advisory fixed before the selected release](https://github.com/advisories/GHSA-q834-8qmm-v933)
