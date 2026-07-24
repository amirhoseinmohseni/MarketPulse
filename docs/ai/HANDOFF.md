# Handoff

## Last updated

2026-07-24

## Current status

Market Insight phases 1 and 2 are complete. `AnalysisGenerator` now orchestrates a provider-neutral, evidence-grounded Application pipeline instead of returning a simulated result.

The pipeline loads only the request's `CollectedMarketItem` records, filters and orders them deterministically, applies configured item/text budgets, assigns temporary IDs such as `C001`, and retains an internal mapping to real database IDs. Application code independently evaluates the maximum allowed signal strength before any provider call.

If no usable item exists, no AI client is called and the generator returns an honest Weak result with a null score and no insights. With usable data, Application builds injection-resistant system/user prompts and a strict JSON Schema, then parses and validates the raw provider response before creating domain entities.

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

## Next task

Implement Market Insight phase 3:

- Add the OpenRouter `IAiMarketInsightClient` implementation in Infrastructure.
- Send the Application-provided system/user prompts and JSON Schema using strict structured output.
- Read endpoint, API key, model, temperature, and token limits from configuration.
- Register the provider through DI without adding OpenRouter dependencies to Application.
- Add provider transport, malformed envelope, timeout, and cancellation tests.

Do not move response trust or evidence validation into Infrastructure. The provider must return raw model JSON to the existing Application validator.

## Verification

- Build: `dotnet build MarketPulse.sln --no-restore -p:NuGetAudit=false` passed with 0 warnings and 0 errors.
- Tests: 18 unit/model/pipeline tests passed.
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
- `MarketPulse.Application/Services/Analyser/IAiMarketInsightClient.cs`
- `MarketPulse.Application/Services/Analyser/AnalysisGenerator.cs`
- `MarketPulse.Application/Services/Analyser/MarketInsightInputBuilder.cs`
- `MarketPulse.Application/Services/Analyser/MarketInsightSignalEvaluator.cs`
- `MarketPulse.Application/Services/Analyser/MarketInsightPromptBuilder.cs`
- `MarketPulse.Application/Services/Analyser/MarketInsightResponseParser.cs`
- `tests/MarketPulse.UnitTests/`
