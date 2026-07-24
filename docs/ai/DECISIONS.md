# Decisions

## Current decisions

### Clean Architecture-style separation

- **Decision:** Keep API, Application, Domain, and Infrastructure as separate projects.
- **Reason:** Isolate business workflows from transport, persistence, and provider-specific code.
- **Consequence:** New external integrations should be implemented in Infrastructure behind Application or Domain contracts.

### OpenRouter behind an abstraction

- **Decision:** Use OpenRouter as the current LLM provider through `IAiSearchQueryClient`.
- **Reason:** Keep provider details out of controllers and application workflows.
- **Consequence:** Provider configuration is mapped in Infrastructure and supplied through dependency injection.

### Application owns Market Insight grounding and validation

- **Decision:** `IAiMarketInsightClient` accepts provider-neutral prompts and a JSON Schema, while Application selects the collected data, evaluates signal quality, validates raw model JSON, and maps temporary evidence IDs to database IDs.
- **Reason:** Structured provider output improves format reliability but is not a trust boundary. Grounding and database integrity must remain independent of OpenRouter behavior.
- **Consequence:** Infrastructure must not create domain insights or evidence directly. Unknown evidence IDs, invalid scores, empty insight text, excessive counts, and signal upgrades are rejected or constrained before persistence.

### Temporary evidence identifiers in AI input

- **Decision:** AI input uses deterministic request-local IDs such as `C001`; real `CollectedMarketItemId` values remain inside Application.
- **Reason:** Short stable identifiers reduce prompt size and prevent the model from inventing or directly handling database identifiers.
- **Consequence:** Every response evidence ID is checked against the exact input allow-list before an `AnalysisEvidence` foreign key is created.

### Strict OpenRouter structured-output routing

- **Decision:** Market Insight requests send the Application-owned JSON Schema through `response_format` with `strict=true` and set `provider.require_parameters=true`.
- **Reason:** OpenRouter can otherwise route to a provider that ignores unsupported parameters, weakening the structured-output guarantee.
- **Consequence:** Requests fail when no compatible route exists instead of silently accepting a provider that cannot enforce the schema.

### Bounded provider retry without sensitive response logging

- **Decision:** Retry only HTTP 429 and 503, honor `Retry-After`, cap retry attempts from configuration, and never include full provider bodies or prompts in logs/exceptions.
- **Reason:** Rate limiting and provider unavailability are transient, while authentication and validation failures need operator action. Provider bodies and prompts may contain sensitive collected data.
- **Consequence:** Retry is cancellation-aware and finite; other HTTP errors fail immediately with only status and sanitized error-code metadata.

### Worker delegates to an Application processor

- **Decision:** Keep queue consumption in `AnalysisWorker` and move per-request orchestration into scoped `IAnalysisRequestProcessor`.
- **Reason:** Status transitions, failure handling, cancellation, and idempotency need focused tests without running a hosted-service loop.
- **Consequence:** The worker resolves one Application processor per queue item and has no direct dependency on AI clients, OpenRouter, repositories, or collectors.

### Atomic and idempotent final completion

- **Decision:** Infrastructure owns `IAnalysisProcessingStateStore`; it locks/rechecks the request and saves the complete result graph plus Completed status in one transaction.
- **Reason:** A request must never expose Completed without its result/evidence, and redelivery must not create a second result.
- **Consequence:** The one-to-one database relationship remains the final uniqueness guard, evidence ownership is revalidated, and failure-state updates clear rolled-back tracked entities first.

### Evidence reason is derived, not provider metadata

- **Decision:** The API `Reason` field is derived from persisted insight text supporting a collected item; source/title/URL/permalink are read from `CollectedMarketItem`.
- **Reason:** The phase 1 schema stores evidence relationships but no independent provider-authored reason field.
- **Consequence:** No migration is required, and the API never trusts provider-returned source metadata.

### Background analysis processing

- **Decision:** Queue analysis request IDs and process them with the hosted `AnalysisWorker`.
- **Reason:** Analysis and external data collection may take longer than a normal HTTP request.
- **Consequence:** The create endpoint returns an accepted request ID and clients must retrieve status/result later.

### Configurable data collectors

- **Decision:** Select collectors through `DataCollectors:Sources:<Source>:Enabled`.
- **Reason:** Providers can be enabled or disabled without changing orchestration code.
- **Consequence:** Collector registration, source naming, and configuration keys must remain aligned.

## Historical or future decisions

No additional architectural decisions have been formally recorded yet. Add significant decisions here or create a numbered ADR under `docs/ai/adr/`.
