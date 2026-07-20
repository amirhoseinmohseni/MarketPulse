# Tasks

This file tracks repository work. Update it when a task starts or is completed.

## Completed

- [x] Initial .NET solution and Clean Architecture-style project separation.
- [x] PostgreSQL persistence and initial EF Core migrations.
- [x] Analysis request creation and retrieval endpoints.
- [x] Background processing queue and worker.
- [x] OpenRouter-backed search query generation abstraction.
- [x] Hacker News collector.
- [x] Reddit client and collector implementation.

## In progress

- [ ] No active task recorded. See `HANDOFF.md`.

## Planned

- [ ] Replace the simulated `AnalysisGenerator` with a real evidence-based analysis flow.
- [ ] Add automated unit and integration test projects.
- [ ] Add robust validation and structured parsing for AI responses.
- [ ] Improve retry, timeout, rate-limit, and failure handling for external providers.
- [ ] Decide and implement the remaining MVP capabilities: sentiment, competitor extraction, pain points, and scoring.
- [ ] Add Product Hunt integration if it remains in product scope.
- [ ] Add production observability and CI/CD.

## Blockers and dependencies

- Real AI analysis depends on an agreed output contract and prompt/schema design.
- Reddit collection depends on valid Reddit credentials and provider access.
- Integration tests require a reproducible PostgreSQL test environment.
