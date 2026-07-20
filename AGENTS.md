# AGENTS.md

## Project context

This project is a .NET Clean Architecture application called MarketPulse.

The application analyzes product or startup ideas by generating search queries, collecting public market data, and producing useful insights.

Before starting a non-trivial task, read the relevant project context documents:

- `docs/ai/PROJECT-CONTEXT.md`
- `docs/ai/ARCHITECTURE.md`
- `docs/ai/CURRENT-STATE.md`
- `docs/ai/HANDOFF.md`

Use `docs/ai/TASKS.md` for planned work and `docs/ai/DECISIONS.md` for recorded architectural decisions. Keep `docs/ai/HANDOFF.md` current when work is paused or handed off to another machine or agent.

## Architecture rules

- Follow the existing Clean Architecture structure.
- Keep external AI provider code behind an abstraction.
- Do not call OpenRouter directly from controllers.
- Prefer interfaces in the Application layer and implementations in the Infrastructure layer.
- Do not hardcode API keys, model names, or endpoints in source code.

## OpenRouter integration

When implementing AI features, use the `openrouter-api` skill.

Use OpenRouter as the LLM provider.

Configuration should come from appsettings, environment variables, or user secrets.

Expected configuration shape:

```json
{
  "OpenRouter": {
    "ApiKey": "",
    "Model": "openrouter/auto",
    "Endpoint": "https://openrouter.ai/api/v1/chat/completions",
    "Temperature": "0.2",
    "MaxTokens": "800"
  }
}
