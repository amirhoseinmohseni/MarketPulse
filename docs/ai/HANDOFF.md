# Handoff

## Last updated

2026-08-21

## Current status

All four Market Insight phases are complete. `AnalysisWorker` now delegates queue items to `IAnalysisRequestProcessor`, which orchestrates query reuse/generation, collection, the evidence-grounded `IAnalysisGenerator`, and final state persistence. The worker has no direct AI/OpenRouter dependency.

Search-query generation now sends an Application-owned strict JSON Schema through OpenRouter structured output. Application requires the exact response object/item shape, filters only semantically invalid individual queries (blank text, invalid category/priority, or case-insensitive duplicates), then rejects the response unless 8-12 valid unique queries remain and all four categories are represented.

`MarketPulse.IntegrationTests` now starts one disposable PostgreSQL 18 container per xUnit collection, runs the real EF migrations, disables parallel execution for the shared database, and truncates application tables before each test. No development connection string or fixed database password is used. Thirty integration tests cover `AnalysisProcessingStateStore` and all repository implementations, including concurrent claims, transaction rollback, evidence ownership, graph loading, request-scoped duplicate checks, and real unique constraints.

The integration suite found and fixed a priority-ordering defect in `SearchQueryRepository`: priority 1 is highest, so generated queries are now read in ascending numeric priority order.

Data-collector test phases 3-5 add 68 deterministic unit tests for collector selection/orchestration, search-query persistence, Hacker News, and Reddit. All HTTP behavior uses controlled handlers; Reddit credentials and tokens used in tests are generated at runtime. The tests cover mapping, limits, URL encoding, deduplication, persistence boundaries, cancellation, OAuth token caching/refresh, deterministic concurrent token requests, and sanitized failures without calling live providers.

The collector tests found that raw external response bodies and exception messages could reach exceptions, orchestration results, or logs. Hacker News and Reddit HTTP failures now expose status only, and non-fail-fast orchestration returns and logs a generic collector failure without the original exception object.

The pipeline loads only the request's `CollectedMarketItem` records, filters and orders them deterministically, applies configured item/text budgets, assigns temporary IDs such as `C001`, and retains an internal mapping to real database IDs. Application code independently evaluates the maximum allowed signal strength before any provider call.

If no usable item exists, no AI client is called and the generator returns an honest Weak result with a null score and no insights. With usable data, Application builds injection-resistant system/user prompts and a strict JSON Schema, then parses and validates the raw provider response before creating domain entities.

## Phase 3 provider behavior

- The OpenRouter Chat Completions request contains exactly the Application system and user messages.
- `response_format.type` is `json_schema`; the Application schema is forwarded unchanged with `strict=true`.
- `provider.require_parameters=true` prevents routing to providers that ignore structured-output parameters.
- API key, endpoint, model, temperature, token limit, timeout, retry count, and base retry delay come from configuration.
- Only HTTP 429 and 503 are retried, with a finite configured retry count.
- Standard `Retry-After` delta/date values take precedence over exponential fallback delay.
- HTTP errors, HTTP-200 error envelopes, choices, finish reason, refusal, content type, empty content, malformed JSON, and truncated output are checked defensively.
- Prompts, collected content, API keys, provider response bodies, and full generated responses are not logged.

## Phase 4 processing behavior

- `IAnalysisProcessingStateStore` conditionally claims Pending/Failed requests and skips Completed or currently Processing requests.
- Existing search queries are reused during reprocessing.
- Result, insight/evidence graph, and Completed status are persisted in one explicit transaction.
- The final write locks the request row and rechecks for an existing result before insertion.
- The EF one-to-one unique relationship remains a database-level duplicate-result safeguard.
- Every evidence FK is revalidated against collected items owned by the same request before persistence.
- A failed final write rolls back; the change tracker is cleared before the request is marked Failed.
- Provider and validation failures mark the request Failed.
- Shutdown cancellation propagates out of collectors/provider/processor and does not mark the request Failed.
- No-data requests complete with an honest Weak/null-score result.
- GET evidence metadata comes from `CollectedMarketItem`; `Reason` is built from persisted insight text.

## Phase 1 schema

```text
AnalysisRequest
  -> AnalysisResult
       -> AnalysisInsight
            -> AnalysisEvidence
                 -> CollectedMarketItem
```

- `AnalysisInsight.Type` distinguishes Strength, Weakness, Opportunity, and Risk.
- `AnalysisInsight.Position` preserves deterministic ordering within each category.
- `AnalysisEvidence` has a real foreign key to `CollectedMarketItem`.
- Duplicate evidence references within one insight are prevented by a unique index.
- Database check constraints protect score, signal strength, insight type, and position ranges.

## Migration behavior

`AddEvidenceBasedMarketInsights` creates the relational insight/evidence schema before removing the legacy text columns. Existing semicolon-delimited Strengths, Weaknesses, Opportunities, and Risks are split and copied into ordered `AnalysisInsight` rows.

Legacy results are assigned `SignalStrength.Weak`. No evidence is fabricated for legacy insights because the old rows contain no traceable source relationship. The down migration reconstructs the legacy text fields before dropping the new tables.

## Phase 2 validation behavior

- MarketScore must be null or between 0 and 100.
- SignalStrength must be Weak, Moderate, or Strong and is capped by Application's deterministic assessment.
- A final Weak signal cannot retain a market score.
- Summary and insight text must be non-empty and within configured limits.
- Insight and evidence counts are bounded.
- Every insight must reference at least one unique temporary evidence ID.
- Unknown or fabricated evidence IDs reject the entire response.
- Only validated IDs are converted to `CollectedMarketItemId` foreign keys.
- Weak input volume or diversity is explicitly reflected in the persisted summary.

## Configuration

Application limits and signal thresholds are read from `MarketInsightAnalysis` configuration. Checked-in defaults cover item count, idea/source/title/content lengths, total item characters, signal thresholds, insight counts, evidence counts, and response text lengths.

OpenRouter transport settings are read from `OpenRouter`. In addition to the existing endpoint/model/temperature/token settings, phase 3 adds `TimeoutSeconds`, `MaxRetryAttempts`, and `RetryBaseDelayMilliseconds`. Docker Compose and `.env.example` contain placeholder/default mappings only.

## Next task

Persistence and data-collector coverage are complete. The next test increment should cover the API/worker boundary and the complete queued analysis workflow; the next runtime reliability increment remains durable delivery and stale-Processing recovery.

## Verification

- Build: `dotnet build MarketPulse.sln --no-restore -p:NuGetAudit=false` passed with 0 warnings and 0 errors.
- Tests: 128 unit/model/pipeline/provider/processor/API/collector tests passed, including 68 tests added for phases 3-5.
- PostgreSQL integration tests: 30 passed against disposable PostgreSQL 18 with real migrations.
- EF model: `dotnet ef migrations has-pending-model-changes` reported no pending changes.
- Migration SQL: forward script generation from `AddCollectedMarketItems` to `AddEvidenceBasedMarketInsights` passed.
- Runtime migration against disposable PostgreSQL 18 passed as part of the integration fixture and infrastructure tests.

## Important files

- `MarketPulse.Domain/Entities/AnalysisResult.cs`
- `MarketPulse.Domain/Entities/AnalysisInsight.cs`
- `MarketPulse.Domain/Entities/AnalysisEvidence.cs`
- `MarketPulse.Infrastructure/Persistence/ApplicationDbContext.cs`
- `MarketPulse.Infrastructure/Persistence/Migrations/20260724163611_AddEvidenceBasedMarketInsights.cs`
- `MarketPulse.Application/Dtos/AnalysisResultDto.cs`
- `MarketPulse.Application/Services/Analyser/IAiMarketInsightClient.cs`
- `MarketPulse.Application/Services/Analyser/AnalysisGenerator.cs`
- `MarketPulse.Application/Services/Analyser/MarketInsightInputBuilder.cs`
- `MarketPulse.Application/Services/Analyser/MarketInsightSignalEvaluator.cs`
- `MarketPulse.Application/Services/Analyser/MarketInsightPromptBuilder.cs`
- `MarketPulse.Application/Services/Analyser/MarketInsightResponseParser.cs`
- `MarketPulse.Application/Services/AnalysisProcessing/AnalysisRequestProcessor.cs`
- `MarketPulse.Application/Services/SearchQueryGenerator/AiQueryGenerator.cs`
- `MarketPulse.Application/Services/SearchQueryGenerator/InvalidAiSearchQueryResponseException.cs`
- `MarketPulse.Application/Workers/AnalysisWorker.cs`
- `MarketPulse.Infrastructure/AI/OpenRouterAiSearchQueryClient.cs`
- `MarketPulse.Infrastructure/AI/OpenRouterAiMarketInsightClient.cs`
- `MarketPulse.Infrastructure/AI/OpenRouterOptions.cs`
- `MarketPulse.Application/Services/DataCollection/DataCollectionOrchestrator.cs`
- `MarketPulse.Infrastructure/HackerNews/HackerNewsClient.cs`
- `MarketPulse.Infrastructure/Reddit/RedditAccessTokenProvider.cs`
- `MarketPulse.Infrastructure/Reddit/RedditClient.cs`
- `MarketPulse.Infrastructure/Persistence/AnalysisProcessingStateStore.cs`
- `tests/MarketPulse.IntegrationTests/Infrastructure/PostgreSqlIntegrationFixture.cs`
- `tests/MarketPulse.IntegrationTests/Persistence/AnalysisProcessingStateStoreTests.cs`
- `tests/MarketPulse.IntegrationTests/Persistence/RepositoryIntegrationTests.cs`
- `tests/MarketPulse.UnitTests/Application/DataCollectionOrchestratorTests.cs`
- `tests/MarketPulse.UnitTests/Application/DataCollectorFactoryTests.cs`
- `tests/MarketPulse.UnitTests/Application/SearchQueryGenerationServiceTests.cs`
- `tests/MarketPulse.UnitTests/Infrastructure/HackerNewsClientTests.cs`
- `tests/MarketPulse.UnitTests/Infrastructure/HackerNewsDataCollectorTests.cs`
- `tests/MarketPulse.UnitTests/Infrastructure/RedditAccessTokenProviderTests.cs`
- `tests/MarketPulse.UnitTests/Infrastructure/RedditClientTests.cs`
- `tests/MarketPulse.UnitTests/Infrastructure/RedditDataCollectorTests.cs`
- `tests/MarketPulse.UnitTests/`
