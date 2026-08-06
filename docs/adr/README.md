# Architecture decision records

Short records of the decisions that shape this codebase: the context, what was decided, what was rejected, and what it costs. Format is a trimmed [MADR](https://adr.github.io/madr/). ADRs are **immutable** — supersede rather than edit.

| # | Decision | Status |
|---|---|---|
| [0001](0001-server-side-credential-custody.md) | The browser never holds an Azure credential | Accepted |
| [0002](0002-direct-webrtc-media-plane.md) | Avatar media bypasses the server | Accepted |
| [0003](0003-shared-cookie-authentication.md) | One shared credential, app-level cookie auth | Accepted, with known limits |
| [0004](0004-startup-only-config-validation.md) | Config validated at startup, no hot reload | Accepted |
| [0005](0005-per-instance-session-cap.md) | Concurrency capped per instance, in memory | Accepted, with a scale-out trap |
| [0006](0006-region-pinned-swedencentral.md) | Region pinned to `swedencentral` | Accepted |
