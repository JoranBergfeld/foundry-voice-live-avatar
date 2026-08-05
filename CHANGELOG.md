# Changelog

All notable changes to this project are documented here. The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

## [Unreleased]

### Added
- Production deployment guide covering identity, secrets, capacity, cost, observability, environments, rollback, DR, networking and data handling (`docs/production-deployment.md`).
- Authoritative wire-protocol reference for `/ws/session`, including frame payload shapes (`docs/wire-protocol.md`).
- Session flow document covering the turn lifecycle, the six status channels (`connection`, `webrtc`, `microphone`, `turn`, `speech`, `avatar`) and per-view journeys (`docs/session-flow.md`), which also gives the previously orphaned diagrams a home.
- Six architecture decision records (`docs/adr/`).
- Threat model with explicit trust assumptions and accepted risks (`docs/threat-model.md`).
- "Why this exists", "Non-goals", "Production readiness" and "Development" sections in the README.
- `SECURITY.md`, `CONTRIBUTING.md`, `CODE_OF_CONDUCT.md`, `CHANGELOG.md`.
- Automated documentation-drift tests (`web/tests/VoiceLive.Web.Tests/DocumentationTests.cs`).
- Documentation index organised by Diátaxis (`docs/README.md`).

### Changed
- Corrected the reconnect claim: reconnection is operator-initiated, not automatic.
- Corrected the autoplay claim: blocked autoplay ends the session; it does not show a recoverable banner.
- Described the actual Content-Security-Policy instead of calling it strict.
- Reconciled RBAC role names against the role GUIDs in `infra/resources.bicep`.
- Renamed `licence.md` to `LICENSE.md` so licence-detection tooling finds it.
- Moved unmaintained material under `docs/history/`: `docs/initial-spec.md` is now `docs/history/initial-spec.md`, and `docs/superpowers/` is now `docs/history/superpowers/`. External links to the old paths will break.

### Removed
- Published development credentials from `README.md`, `web/README.md`, `docs/runbook.md`, `docs/config-schema.md` and `appsettings.Development.json`; replaced with `dotnet user-secrets` instructions.
- `agentVersion`, `conversationResumePolicy` and `groundingStrategy` from `config/agent.json` and the schema — no code reads them.
- `azure-custom` from the documented `voice.type` values — session creation always fails on it.
- Point-in-time end-to-end test evidence from the runbook.
