# 0003 — One shared credential, app-level cookie authentication

**Status:** Accepted, with known limits

## Context

The app needs to keep the public internet away from a per-minute-billed Azure session. Its users are a handful of named people running one rehearsed event.

## Decision

A single shared username and password, validated by app middleware, issuing an 8-hour sliding cookie. No user store, no identity provider.

## Alternatives rejected

- **Microsoft Entra ID via App Service Easy Auth.** The correct long-term answer — per-operator identity, revocation, no credential custody in the app. Rejected for the initial build because it requires tenant configuration outside the repository and adds a sign-in dependency on event day, when the failure mode of an identity outage is a dead show.
- **No authentication at all.** Unacceptable: an unauthenticated public endpoint on a per-minute-billed service is a cost and abuse incident waiting to happen.

## Consequences

- **This is the entire authorization model.** The authorization middleware (`Program.cs`) checks only `ctx.User.Identity?.IsAuthenticated` — every authenticated user reaches every endpoint, including `say`, which puts arbitrary text in the avatar's mouth on stage (finding H-01).
- No audit trail attributable to a person. "Who made it say that" has no answer.
- **Revoking a session is not possible by changing credentials.** The authorization check does not re-validate credentials after sign-in. Changing the shared password or username does not invalidate live cookies, and because the 8-hour expiry is **sliding**, an active tab renews the cookie on each request and never ages out on its own. The only revocation path is destroying the ASP.NET Data Protection key ring. On App Service, the key ring persists to `%HOME%\ASP.NET\DataProtection-Keys` (a network-backed share), so restarting the app does **not** revoke sessions; only destroying the key ring does.
- Consequently the app **must not be left internet-facing** — but nothing enforces that. `azd up` provisions a public App Service with no IP restrictions or VNet integration, so out of the box the shared password is the only access control. Adding App Service access restrictions is the operator's responsibility; see [`../production-deployment.md`](../production-deployment.md) §9.
- Superseding this ADR with Entra ID is the single highest-value security change available.
