# Handoff

## Last updated

2026-07-24

## Current status

Market Insight phase 1 is complete. `AnalysisResult` now has a nullable market score, an explicit `SignalStrength`, and a relational collection of typed insights. Each `AnalysisInsight` can reference one or more real `CollectedMarketItem` records through `AnalysisEvidence`.

The final analysis generator is still a placeholder. It now returns an honest Weak result with a null score and no fabricated insights so the existing pipeline remains compatible with the new contract; real evidence-grounded generation belongs to phase 2.

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

## Next task

Implement Market Insight phase 2 in the Application layer:

- Define `IAiMarketInsightClient` and provider-neutral request/response contracts.
- Load only the request's collected market items.
- Build bounded deterministic AI input.
- Evaluate and enforce weak-signal policy.
- Build the grounded prompt.
- Parse and validate structured output and evidence IDs.
- Replace the simulated generator with the real Application orchestration.

Do not add OpenRouter-specific code to Domain or Application. The OpenRouter provider remains phase 3.

## Verification

- Build: `dotnet build MarketPulse.sln --no-restore -p:NuGetAudit=false` passed with 0 warnings and 0 errors.
- Tests: 7 unit/model mapping tests passed.
- EF model: `dotnet ef migrations has-pending-model-changes` reported no pending changes.
- Migration SQL: forward script generation from `AddCollectedMarketItems` to `AddEvidenceBasedMarketInsights` passed.
- Runtime migration against a live PostgreSQL database was not run.

## Important files

- `MarketPulse.Domain/Entities/AnalysisResult.cs`
- `MarketPulse.Domain/Entities/AnalysisInsight.cs`
- `MarketPulse.Domain/Entities/AnalysisEvidence.cs`
- `MarketPulse.Infrastructure/Persistence/ApplicationDbContext.cs`
- `MarketPulse.Infrastructure/Persistence/Migrations/20260724163611_AddEvidenceBasedMarketInsights.cs`
- `MarketPulse.Application/Dtos/AnalysisResultDto.cs`
- `tests/MarketPulse.UnitTests/`
