# Architecture

## Repository layout

```text
MarketPulse.Api/             HTTP entry point and composition root
MarketPulse.Application/     Use cases, DTOs, abstractions, orchestration
MarketPulse.Domain/          Entities, enums, repository contracts
MarketPulse.Infrastructure/ External services, persistence, repositories
docs/ai/                     AI-oriented project context
```

## Layer responsibilities

### Domain

Contains core entities, enums, and repository interfaces. It should not depend on ASP.NET Core, database providers, or external APIs.

Important domain entities include `AnalysisRequest`, `AnalysisResult`, `SearchQuery`, `RedditPost`, and `CollectedMarketItem`.

### Application

Contains application services, DTOs, interfaces, data-collection orchestration, search-query generation, and the `AnalysisWorker`. It coordinates use cases without knowing provider-specific implementation details.

### Infrastructure

Contains `ApplicationDbContext`, EF Core migrations, repository implementations, background queue implementation, OpenRouter integration, Hacker News integration, Reddit integration, and configuration mapping.

### API

Contains the ASP.NET Core host, dependency-injection composition, controllers, Swagger, and health-check endpoint. Controllers should call Application abstractions and must not call OpenRouter or other external providers directly.

## Dependency direction

```text
Application    --> Domain
Infrastructure --> Application, Domain
API            --> Application, Infrastructure
```

The arrows show project references. Infrastructure implements contracts defined by Domain or Application. API is the composition root and wires Application and Infrastructure together in `Program.cs`.

## Current request flow

`POST /api/analysis` calls `IAnalysisRequestService`, persists a pending request, and queues its ID. `AnalysisWorker` creates a scope, loads repositories and services, generates queries, collects enabled sources, generates an analysis result, and updates request status. `GET /api/analysis/{id}` reads the request with its result and maps it to a DTO.

## Adding new features

- Put business concepts and persistence contracts in Domain.
- Put use-case interfaces, DTOs, orchestration, and provider-neutral logic in Application.
- Put provider clients, database code, repositories, and configuration adapters in Infrastructure.
- Keep API changes limited to transport concerns and dependency wiring.
- Add external AI or data providers behind an interface; register implementations through dependency injection.

## AI provider rules

- OpenRouter access belongs in Infrastructure.
- Application depends on `IAiSearchQueryClient`, not `HttpClient` details or OpenRouter types.
- API controllers must not call OpenRouter directly.
- API keys, endpoints, models, temperature, and token limits must come from configuration or environment variables.
