# foundry-voice-live-avatar

A conversational avatar on Microsoft Foundry's Voice Live API, built as **two independent apps**:

- [`/cli`](./cli) - voice-only rehearsal harness (fast prompt/turn-taking/voice tuning). Windows for audio.
- [`/web`](./web) - the on-stage show client: token-broker backend + browser avatar via `@azure/ai-voicelive`.
- [`/config`](./config) - shared runtime configuration (no code). Both apps validate it at startup.
- [`/tools`](./tools) - `sync-agent`: promotes grounding + Voice Live config into the Foundry agent.
- [`/docs`](./docs) - spec, config schema, runbook, rehearsal checklist.

Design spec: `docs/superpowers/specs/2026-07-22-voice-live-avatar-design.md`.

> Deployment is a trusted operator machine with no web auth; the browser holds only a short-lived Entra token.
