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
