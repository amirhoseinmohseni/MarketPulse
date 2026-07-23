# Handoff

## Last updated

2026-07-20

## Current status

The repository is a .NET 8 Clean Architecture-style API. Analysis requests are persisted, queued, processed by `AnalysisWorker`, enriched with AI-generated search queries and enabled data collectors, and exposed through `POST /api/analysis` and `GET /api/analysis/{id}`. The final `AnalysisGenerator` output is still simulated with a random score and static text.

## Next task

Define the real analysis output contract, implement evidence-based analysis behind an Application abstraction with an Infrastructure OpenRouter provider, and add tests for parsing, provider failures, and request status transitions.

## Verification

- Build: `dotnet build MarketPulse.sln --no-restore` passed with 0 warnings and 0 errors.
- Tests: no test project or test files currently exist.
- Runtime/API smoke test: not run.

## Important files

- `MarketPulse.Application/Services/Analyser/AnalysisGenerator.cs`
- `MarketPulse.Application/Workers/AnalysisWorker.cs`
- `MarketPulse.Application/Services/SearchQueryGenerator/`
- `MarketPulse.Infrastructure/AI/`
- `MarketPulse.Infrastructure/Persistence/ApplicationDbContext.cs`
