# 0004 — Config validated at startup, no hot reload

**Status:** Accepted

## Context

Behaviour comes from JSON files in `config/`. They could be watched and reloaded at runtime.

## Decision

Config is read and validated **once, at startup**. A `WebConfigValidationException` during loading is caught; the app starts and reports the problem through `/api/health` (503 Unhealthy) rather than crashing. There is no file watcher and no reload endpoint.

## Alternatives rejected

- **Hot reload.** Mid-show config changes are a footgun: a typo silently changes avatar behaviour in front of an audience, with no review and no rollback.
- **Fail-fast exit on invalid config.** Rejected because a crash-looping app on event day gives an operator nothing to diagnose with. Starting unhealthy-but-reachable means `/api/health` can explain the problem.

## Consequences

- `/api/health` is the authoritative readiness signal, not "the process is running". Alert on it (see [`../production-deployment.md`](../production-deployment.md) §5).
- **Changing config requires a restart, which drops every live session.** Never edit `config/` during a show; treat config changes as deployments.
- Config errors are caught in rehearsal rather than at the first session — provided someone actually checks `/api/health`.
