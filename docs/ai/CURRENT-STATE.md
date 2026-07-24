# Current State

## Verified on

2026-07-24

## Complete or implemented

- The solution contains `MarketPulse.Api`, `MarketPulse.Application`, `MarketPulse.Domain`, and `MarketPulse.Infrastructure`.
- The solution targets .NET 8.
- `POST /api/analysis` creates and queues an analysis request.
- `GET /api/analysis/{id}` returns request status and a stored result when available.
- `/health` is mapped and Swagger is enabled in Development.
- PostgreSQL persistence and EF Core migrations are present.
- An in-process background queue and `AnalysisWorker` are registered.
- AI search query generation is implemented behind Application interfaces and uses OpenRouter in Infrastructure.
- The provider-neutral Market Insight Application pipeline is implemented. It builds bounded deterministic input, evaluates signal quality, creates a grounded prompt and strict response schema, validates AI JSON, and maps temporary evidence IDs to real collected item IDs.
- Empty or unusable collected datasets complete the analysis-generation step without an AI call and produce an honest Weak result with a null score.
- Hacker News data collection is implemented and enabled by the checked-in default configuration.
- Reddit integration is implemented but disabled by default.
- The solution includes unit and EF model tests.
- `dotnet build MarketPulse.sln --no-restore` passed with 0 warnings and 0 errors on the verification date.

## Incomplete or not yet integrated

- `IAiMarketInsightClient` has no Infrastructure implementation or DI registration yet. Until phase 3 is complete, the real worker flow cannot resolve and call a Market Insight provider.
- OpenRouter structured-output transport for Market Insight is not implemented.
- Worker idempotency, cancellation-specific status handling, and atomic completion/result persistence remain phase 4 work.
- Authentication, authorization, frontend, payments, and multi-tenancy are not implemented.
- Product Hunt integration is described in the README vision but is not present in the current source tree.
- Observability, retry/rate-limit strategy, and production-grade resilience are not fully implemented.

## Known configuration state

- `DataCollectors:Sources:HackerNews:Enabled` defaults to `true`.
- `DataCollectors:Sources:Reddit:Enabled` defaults to `false`.
- OpenRouter and Reddit credentials are expected through configuration/environment variables; do not commit real values.
- Local Docker Compose runs PostgreSQL, an EF migration container, and the API.

## Areas requiring care

- Do not bypass Application abstractions from controllers.
- Changes to entities, EF mappings, or migrations can affect existing database data.
- The worker changes request status and persists collected data asynchronously; status transitions must remain consistent.
- External API behavior, credentials, rate limits, and failure handling need explicit verification before changing integrations.
- AI output remains untrusted after structured generation; Application validation and evidence-ID allow-listing must not be bypassed by future providers.
