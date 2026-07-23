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
```

Environment variables use the standard .NET double-underscore format:

```text
OpenRouter__ApiKey
OpenRouter__Model
OpenRouter__Endpoint
OpenRouter__Temperature
OpenRouter__MaxTokens
ConnectionStrings__DefaultConnection
```

## Secrets and local configuration

- Never commit API keys, tokens, passwords, connection strings, or other secrets.
- Never hardcode secrets in source code, Dockerfiles, Docker Compose files, tracked configuration, tests, or documentation.
- Use environment variables, .NET user secrets, CI/CD secrets, or read-only bind-mounted secret files.
- Keep local secrets in an untracked `.env` file or an ignored `secrets/` directory.
- Commit only sanitized templates such as `.env.example`, using placeholder values.
- In Docker Compose, reference values with `${VARIABLE_NAME}` and map them to .NET configuration keys where necessary.
- Use the standard .NET `Section__Property` naming format for environment-variable overrides.
- Mount sensitive files as read-only (`:ro`) when bind mounts are required.
- Do not print secrets in logs, exceptions, test output, generated artifacts, or documentation.
- Before committing configuration changes, verify that tracked files contain no real credentials.
- If a committed secret is discovered, rotate or revoke it immediately before removing it from Git history.
