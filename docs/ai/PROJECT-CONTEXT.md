# MarketPulse Project Context

## Purpose

MarketPulse is an AI-assisted product validation and market intelligence backend. A user submits a product or startup idea, and the system creates search queries, collects public market signals, and stores an evidence-grounded Market Insight result.

## Problem

Early product research is time-consuming and difficult to perform consistently. MarketPulse is intended to turn public discussions and market signals into structured evidence about demand, competition, risks, and opportunities.

## Primary users and scenarios

- Founders validating an early product idea.
- Indie hackers evaluating demand before implementation.
- Product teams researching pain points, alternatives, and competitors.

The currently implemented user flow is:

1. Submit an idea through the analysis API.
2. Persist an `AnalysisRequest` with `Pending` status.
3. Queue the request for background processing.
4. Generate AI-assisted search queries.
5. Collect data from enabled public sources.
6. Generate and persist an analysis result.
7. Retrieve the request and result by ID.

## Implemented capabilities

- ASP.NET Core API for creating and reading analysis requests.
- Background analysis processing through an in-process queue and hosted worker.
- AI search-query generation through an Application abstraction and OpenRouter Infrastructure implementation.
- Evidence-grounded Market Insight generation through a provider-neutral Application pipeline and OpenRouter Infrastructure implementation.
- Deterministic signal-quality enforcement and evidence-ID validation.
- Atomic final result/evidence/request completion persistence.
- Configurable data collector pipeline.
- Hacker News Algolia collector.
- Reddit client and collector implementation, currently disabled by configuration.
- PostgreSQL persistence through Entity Framework Core.
- EF Core migrations.
- Health check and development Swagger.

## Intended MVP capabilities

The README describes sentiment analysis, competitor detection, pain-point extraction, market scoring, and AI summaries as MVP goals. Some of these are not fully implemented yet; consult `CURRENT-STATE.md` before treating them as available behavior.

## Technology

- .NET 8 and C#.
- ASP.NET Core Web API.
- Clean Architecture-style project separation.
- Entity Framework Core 8 with PostgreSQL/Npgsql.
- Docker Compose for local API and database orchestration.
- OpenRouter for the current AI provider.
- Hacker News Algolia API and Reddit OAuth/API integrations.
