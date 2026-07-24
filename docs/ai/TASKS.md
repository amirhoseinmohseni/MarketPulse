# Tasks

This file tracks repository work. Update it when a task starts or is completed.

## Completed

- [x] Initial solution and Clean Architecture-style project separation.
- [x] PostgreSQL persistence and initial EF Core migrations.
- [x] Analysis request creation and retrieval endpoints.
- [x] Background processing queue and worker.
- [x] OpenRouter-backed search query generation abstraction.
- [x] Hacker News collector.
- [x] Reddit client and collector implementation.
- [x] Define the evidence-based Market Insight domain contract.
- [x] Add nullable MarketScore and SignalStrength.
- [x] Replace legacy insight strings with typed relational AnalysisInsight records.
- [x] Add AnalysisEvidence with a foreign key to CollectedMarketItem.
- [x] Add a non-destructive legacy backfill migration.
- [x] Add the initial unit and EF model mapping test project.
- [x] Phase 2: define the provider-neutral `IAiMarketInsightClient` contract.
- [x] Phase 2: implement deterministic bounded AI input and temporary evidence IDs.
- [x] Phase 2: evaluate and enforce signal quality independently from the model.
- [x] Phase 2: build grounded, prompt-injection-resistant prompts and a strict JSON Schema.
- [x] Phase 2: parse and validate model JSON, insight limits, scores, and evidence IDs.
- [x] Phase 2: map validated evidence to real `CollectedMarketItemId` values.
- [x] Phase 2: add Application pipeline tests, including cancellation and malformed output.

## In progress

- [ ] No active task recorded. Phase 3 is next.

## Planned

- [ ] Phase 3: implement the OpenRouter Market Insight provider and structured JSON Schema output.
- [ ] Phase 3: add provider error, timeout, and malformed-response tests.
- [ ] Phase 4: harden worker status transitions, cancellation, idempotency, and atomic persistence.
- [ ] Phase 4: add end-to-end PostgreSQL and API integration tests.
- [ ] Decide and implement the remaining MVP capabilities: sentiment, competitor extraction, pain points, and scoring.
- [ ] Add Product Hunt integration if it remains in product scope.
- [ ] Add production observability and CI/CD.

## Blockers and dependencies

- Real Market Insight generation in the worker depends on the phase 3 `IAiMarketInsightClient` implementation and DI registration.
- Phase 3 must use the phase 2 provider-neutral request and return raw model JSON for Application validation.
- Reddit collection depends on valid Reddit credentials and provider access.
- Live migration/integration tests require a reproducible PostgreSQL test environment.
