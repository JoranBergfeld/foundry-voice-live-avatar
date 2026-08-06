# 0005 — Concurrency capped per instance, in memory

**Status:** Accepted, with a scale-out trap

## Context

Voice Live sessions bill per minute and consume avatar-rendering quota. Unbounded concurrency is a cost and quota incident.

## Decision

An in-memory semaphore (`SessionGate`) caps concurrent sessions at `MaxConcurrentSessions`, default **2**, bound from `VoiceLiveOptions` (ASP.NET configuration). Connections beyond the cap have their WebSocket handshake accepted, then immediately receive a text error frame (`"The server is at capacity. Try again shortly."`) and the connection closes. Override the default via the `VoiceLive__MaxConcurrentSessions` app setting.

## Alternatives rejected

- **Distributed cap in Redis or a database.** Correct for a scaled-out deployment, and unjustified infrastructure for a single-instance, single-event app.
- **No cap.** A forgotten tab or a stuck client bills indefinitely.

## Consequences

- **The cap does not survive scale-out.** N instances means N × `MaxConcurrentSessions`, silently — an operator scaling out to "add capacity" removes the control. Scale up, not out. Recorded in [`../production-deployment.md`](../production-deployment.md) §3.
- The default of 2 matches the intended deployment: one operator view and one display view.
- **Each browser tab is a session.** Opening a third tab is rejected, which surprises operators who expect tabs to share.
- There is no session timeout (finding M-01 — "No idle or absolute session timeout; capacity gate trivially exhausted"), so a slot is held until the tab closes or the app restarts. The cap bounds concurrency, not duration.
