# MarketPulse

MarketPulse is a .NET 8 backend for validating product and startup ideas with collected public market signals.

A user submits an idea, the API queues it for background processing, search queries are generated, enabled collectors gather relevant discussions, and an evidence-grounded Market Insight result is generated through OpenRouter.

## Processing flow

```text
POST /api/analysis
        |
        v
Pending AnalysisRequest
        |
        v
In-process background queue
        |
        v
AnalysisWorker -> IAnalysisRequestProcessor
        |
        +--> generate/reuse search queries
        +--> collect market items
        +--> IAnalysisGenerator
                |
                +--> bounded deterministic input
                +--> signal-quality evaluation
                +--> OpenRouter structured output
                +--> response/evidence validation
        |
        v
Atomic AnalysisResult + Evidence + Completed status
```

The worker depends only on the Application processor. OpenRouter is implemented in Infrastructure behind `IAiMarketInsightClient`.

## Market Insight output

`GET /api/analysis/{id}` returns the request status and, when completed, a structured result:

```json
{
  "id": "analysis-request-id",
  "status": 2,
  "result": {
    "marketScore": 64,
    "signalStrength": 1,
    "summary": "Evidence-grounded market summary.",
    "strengths": [
      {
        "id": "insight-id",
        "text": "Users repeatedly describe the target problem.",
        "evidenceIds": ["collected-market-item-id"]
      }
    ],
    "weaknesses": [],
    "opportunities": [],
    "risks": [],
    "evidence": [
      {
        "collectedMarketItemId": "collected-market-item-id",
        "source": "HackerNews",
        "title": "Discussion title from the database",
        "url": "https://example.test/item",
        "permalink": "https://example.test/permalink",
        "reason": "Users repeatedly describe the target problem."
      }
    ]
  }
}
```

Enums currently use their numeric JSON representation (`AnalysisStatus.Completed = 2`, `SignalStrength.Moderate = 1`).

Evidence source metadata is loaded from the stored `CollectedMarketItem`, never trusted from model output. `Reason` is derived from the persisted insight text associated with that evidence.

## Grounding and weak-signal behavior

- The idea is the analysis topic, not evidence.
- AI input contains only selected `CollectedMarketItem` fields.
- Collected content is treated as untrusted data and prompt instructions inside it are ignored.
- Temporary IDs such as `C001` are mapped back to real database IDs inside Application.
- Unknown or fabricated evidence IDs reject the provider result.
- Application independently caps `SignalStrength`; the provider cannot upgrade weak input to Strong.
- With no usable collected data, no Market Insight provider call is made. The request completes with `SignalStrength=Weak`, `MarketScore=null`, and an honest summary.

## Architecture

```text
MarketPulse.Api/             HTTP API and composition root
MarketPulse.Application/     Use cases, processor, AI abstractions and validation
MarketPulse.Domain/          Entities, enums and repository contracts
MarketPulse.Infrastructure/ OpenRouter, collectors, EF Core and repositories
tests/MarketPulse.UnitTests/ Unit, model, provider and processor tests
docs/ai/                     Maintainer context and handoff documentation
```

Dependency direction:

```text
Application    --> Domain
Infrastructure --> Application, Domain
API            --> Application, Infrastructure
```

## Implemented capabilities

- Create and retrieve asynchronous analysis requests.
- In-process queue and hosted background worker.
- OpenRouter-backed search-query generation.
- Hacker News Algolia data collection.
- Reddit client/collector, disabled by default.
- Deterministic bounded Market Insight input.
- Strict OpenRouter JSON Schema output with compatible provider routing.
- Application-side response, score, signal and evidence validation.
- Relational Strength, Weakness, Opportunity and Risk insights.
- Evidence foreign keys to real collected market items.
- Atomic final result/evidence/status persistence.
- Duplicate-result prevention and duplicate queue-delivery handling.
- Cancellation-aware provider retry and worker shutdown.
- PostgreSQL persistence with EF Core migrations.

## Technology

- .NET 8 and ASP.NET Core
- Entity Framework Core 8
- PostgreSQL with Npgsql
- OpenRouter Chat Completions
- Docker and Docker Compose
- xUnit

## API

Create an analysis:

```http
POST /api/analysis
Content-Type: application/json

{
  "idea": "AI note-taking app for students"
}
```

The API responds with `202 Accepted` and an analysis request ID.

Retrieve status/result:

```http
GET /api/analysis/{id}
```

Health check:

```http
GET /health
```

Swagger is enabled in the Development environment.

## Running locally

Requirements:

- Docker with Compose
- or .NET 8 SDK and PostgreSQL

Create a local environment file:

```bash
cp .env.example .env
```

Replace the placeholder PostgreSQL password and OpenRouter API key in `.env`. Do not commit `.env`.

Start the stack:

```bash
docker compose up --build
```

Docker Compose waits for PostgreSQL, runs the EF migration container, and starts the API at:

```text
http://localhost:5000
```

Configuration uses standard .NET environment-variable names such as:

```text
ConnectionStrings__DefaultConnection
OpenRouter__ApiKey
OpenRouter__Model
OpenRouter__Endpoint
OpenRouter__Temperature
OpenRouter__MaxTokens
OpenRouter__TimeoutSeconds
OpenRouter__MaxRetryAttempts
OpenRouter__RetryBaseDelayMilliseconds
```

Secrets must be supplied through environment variables, user secrets, CI/CD secrets, or another untracked local secret source.

## Build and test

```bash
dotnet build MarketPulse.sln --no-restore
dotnet test MarketPulse.sln --no-build --no-restore
```

Check EF model consistency:

```bash
dotnet ef migrations has-pending-model-changes \
  --project MarketPulse.Infrastructure \
  --startup-project MarketPulse.Api
```

## Current MVP limitations

- The queue is process-local and is not durable across application restarts.
- A request cancelled during shutdown remains Processing; automatic stale-job recovery is not implemented.
- Reddit collection requires credentials and is disabled by default.
- Product Hunt collection is not implemented.
- Sentiment, competitor and pain-point extraction are not separate first-class result contracts yet.
- Authentication, authorization, multi-tenancy, payments and a frontend are not implemented.
- Live provider and PostgreSQL integration tests require external services and credentials.
- Production observability and distributed job processing remain future work.

## License

MIT
