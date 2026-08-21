# Current State

## Verified on

2026-08-21

## Complete or implemented

- The solution contains `MarketPulse.Api`, `MarketPulse.Application`, `MarketPulse.Domain`, and `MarketPulse.Infrastructure`.
- The solution targets .NET 8.
- `POST /api/analysis` creates and queues an analysis request.
- `GET /api/analysis/{id}` returns request status and a stored result when available.
- `/health` is mapped and Swagger is enabled in Development.
- PostgreSQL persistence and EF Core migrations are present.
- An in-process background queue and `AnalysisWorker` are registered.
- AI search query generation is implemented behind Application interfaces and uses OpenRouter in Infrastructure.
- Search-query generation uses strict JSON Schema output, removes invalid or case-insensitively duplicated individual queries, and rejects final sets outside 8-12 queries or missing any required category.
- The provider-neutral Market Insight Application pipeline is implemented. It builds bounded deterministic input, evaluates signal quality, creates a grounded prompt and strict response schema, validates AI JSON, and maps temporary evidence IDs to real collected item IDs.
- `IAiMarketInsightClient` is implemented in Infrastructure with OpenRouter Chat Completions, strict JSON Schema output, structured-output-compatible routing, bounded transient retry, configured timeout, and defensive response-envelope validation.
- Empty or unusable collected datasets complete the analysis-generation step without an AI call and produce an honest Weak result with a null score.
- `AnalysisWorker` delegates each queue item to a testable Application processor and never calls OpenRouter or an AI client directly.
- Final `AnalysisResult`, insight/evidence graph, and Completed status are saved in one explicit database transaction.
- Completion rechecks the request under a PostgreSQL row lock, and the one-to-one result relationship prevents duplicate results.
- Provider, validation, and final-persistence failures transition requests to Failed when the database remains available.
- Host-shutdown cancellation is propagated without marking the request Failed.
- GET evidence includes database-backed source/title/URL/permalink metadata plus a reason derived from the persisted supporting insight.
- Hacker News data collection is implemented and enabled by the checked-in default configuration.
- Reddit integration is implemented but disabled by default.
- The solution includes unit and EF model tests.
- `dotnet build MarketPulse.sln --no-restore` passed with 0 warnings and 0 errors on the verification date.
- All 60 unit/model/pipeline/provider/processor/API tests pass.

## Incomplete or not yet integrated

- Authentication, authorization, frontend, payments, and multi-tenancy are not implemented.
- Product Hunt integration is described in the README vision but is not present in the current source tree.
- Observability, retry/rate-limit strategy, and production-grade resilience are not fully implemented.

## Known configuration state

- `DataCollectors:Sources:HackerNews:Enabled` defaults to `true`.
- `DataCollectors:Sources:Reddit:Enabled` defaults to `false`.
- OpenRouter and Reddit credentials are expected through configuration/environment variables; do not commit real values.
- OpenRouter timeout and retry behavior is configured through `OpenRouter:TimeoutSeconds`, `OpenRouter:MaxRetryAttempts`, and `OpenRouter:RetryBaseDelayMilliseconds`.
- Local Docker Compose runs PostgreSQL, an EF migration container, and the API.

## Areas requiring care

- Do not bypass Application abstractions from controllers.
- Changes to entities, EF mappings, or migrations can affect existing database data.
- The worker changes request status and persists collected data asynchronously; status transitions must remain consistent.
- External API behavior, credentials, rate limits, and failure handling need explicit verification before changing integrations.
- AI output remains untrusted after structured generation; Application validation and evidence-ID allow-listing must not be bypassed by future providers.
- Provider responses, prompts, collected content, and API keys must not be added to logs or exception messages.
- The in-process queue is not durable. Shutdown-cancelled requests remain Processing and there is no automatic stale-request recovery yet.
