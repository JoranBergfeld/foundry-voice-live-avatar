# 0006 — Region pinned to `swedencentral`

**Status:** Accepted

## Context

Voice Live features are not uniformly available across Azure regions, and the required combination is narrow.

## Decision

Deploy to `swedencentral`, the region supporting **native realtime voice, avatar rendering and agent mode together**. This is pinned as the default in `infra/main.bicep`. West Europe does not offer the full combination.

## Alternatives rejected

- **Deploy nearest the venue for latency.** Rejected: a region missing avatar or agent mode does not degrade gracefully, it fails. Feature availability beats a few milliseconds.
- **Split resources across regions.** Adds cross-region latency to the media path for no benefit.

## Consequences

- Speech is processed in the EU, which is a **compliance property**, not just a latency one. Changing region changes where attendee speech is processed — see [`../production-deployment.md`](../production-deployment.md) §10.
- Latency is bounded by the venue's distance to Sweden. Measure it during rehearsal from the actual stage network.
- A DR region must be verified to support the same feature combination before being treated as a standby.
- Some regions lack the `DOTNETCORE|10.0` runtime, needing the `LINUX_FX_VERSION` fallback documented in [`../runbook.md`](../runbook.md).
