# Threat model

One page. Actors, assets, entry points, and — most importantly — the assumptions this design **trusts without verifying**. Every unstated assumption in that last table is where a security finding will come from.

**Intended deployment:** operator-attended, single-event, on a trusted network. Deviating from that invalidates most of what follows. See [Production readiness](../README.md#production-readiness).

## Assets

| Asset | Why it matters |
|---|---|
| Azure Foundry credentials | Managed identity access to a paid AI resource. **Highest-value asset.** Never leaves the server ([ADR 0001](adr/0001-server-side-credential-custody.md)). |
| The operator credential | The only thing between the internet and everything below. |
| Voice Live session capacity | Billed per minute, capped at 2 concurrent, with no timeout. Denial of service is also denial of budget. |
| What the avatar says on stage | The reputational asset. Compromise here is visible to a live audience in real time. |
| Attendee speech | Microphone audio sent to Azure (EU region by default). Not persisted by this app. In the default **gated** mode (`turntaking.json: activeMode = "gated"`), `manualTurn` is `true`, so `InputAudioTranscription` is never configured — no user transcripts are produced at all by default. Transcription is only active when a mode with turn detection is selected. |

## Actors

| Actor | Trust | Capability |
|---|---|---|
| Operator | Trusted | Full app access. Runs the show. |
| Authenticated user | **Trusted by the design, and this is the weak point** | Everything the operator can do, including `say`. |
| Network attacker (unauthenticated) | Untrusted | Can reach `/login`, `/logout`, `/api/health`, and forge headers on requests. |
| Audience member | Untrusted | Physical proximity; may be picked up by the microphone. |
| Azure Foundry | Trusted | Generates what the audience sees and hears. |

## Entry points

Authoritative source: [`docs/wire-protocol.md`](wire-protocol.md). Any disagreement between the two documents is a bug here.

| Entry point | Auth | Notes |
|---|---|---|
| `GET /` | Cookie | Application shell (operator, display, landing views). Unauthenticated requests redirect to `/login`. |
| `GET /login` | Anonymous | Sign-in form. |
| `POST /login` | Anonymous | Credential submission. Rate limiting is per-IP and header-forgeable (C-01); no antiforgery (H-02). |
| `POST /logout` | **Anonymous** | Clears the auth cookie. Deliberately on the anonymous allow-list (`Program.cs:95-97`); no antiforgery (H-02). |
| `GET /api/health` | Anonymous | Discloses configuration validity to unauthenticated callers. |
| `GET /api/config` | Cookie | Returns browser-safe config (region, model, voice, avatar settings, turn-taking mode, agent metadata, safe questions). |
| `GET /ws/session` | Cookie + `Origin` check | Consumes a concurrency slot and starts billing. `Origin` validation rejects requests from outside allowed origins (same-origin or configured `AllowedOrigins`); non-browser clients with no `Origin` header are allowed through. |
| `say` WebSocket frame | Cookie (session already open) | **Arbitrary text spoken on stage. Unconstrained (H-01).** |
| `config/*.json` | Filesystem | Anyone who can change these changes avatar behaviour. Deployment-time trust. |

## Assumptions this design trusts without verifying

**This is the section to re-read whenever the deployment changes.**

| Assumption | Status | If false |
|---|---|---|
| The network is trusted and access is limited to the event team | **Not enforced by anything.** `azd up` yields a public endpoint with no IP restrictions | Every row below becomes exploitable by anyone on the internet |
| An authenticated user is benign | **Accepted risk, deliberately** | Arbitrary avatar speech in front of an audience ([H-01](../review-merged.md#h-01--say-control-frame-is-an-unrestricted-prompt-injection-and-cost-channel--high)) |
| The client IP seen by the rate limiter is real | **False today** — forwarded headers are unvalidated | Per-IP login rate limiting is bypassable ([C-01](../review-merged.md#c-01--login-rate-limiter-bypassable-via-spoofed-x-forwarded-for--critical)) |
| One shared credential is sufficient identity | **Accepted for this deployment shape** | No attribution, no per-person revocation ([ADR 0003](adr/0003-shared-cookie-authentication.md)). Changing the password or username does **not** revoke live sessions; the 8-hour **sliding** cookie renews on each request. The only revocation path is destroying the ASP.NET Data Protection key ring — restarting the app does not suffice on App Service. |
| The operator credential is not in source control | **Enforced** — `Development_settings_carry_no_auth_section` fails if `appsettings.Development.json` carries an `Auth` section; `Maintained_markdown_publishes_no_credential_literals` fails if docs publish credential literals | Public credential disclosure ([C-02](../review-merged.md#c-02--working-credentials-committed-to-the-repository--critical)) |
| Config files are only writable by deployers | Deployment-time trust, unverified at runtime | Arbitrary behaviour change with no audit |
| Azure output is safe to show an audience | Trusted; no content filtering in this app | Whatever the model produces reaches the stage |
| `Auth__Password` in App Service settings is protected | **Not enforced** — `infra/resources.bicep:89` writes `Auth__Password` as a plaintext app setting on every `azd up`, clobbering any Key Vault reference set between provisions ([M-02](../review-merged.md)) | The credential is visible as a plaintext App Service setting after any re-provision |

## Accepted risks

Stated so they are decisions rather than oversights:

1. **Any authenticated user can make the avatar say anything.** Accepted because the authenticated population is the event team. Unacceptable the moment that population grows — fix [H-01](../review-merged.md#h-01--say-control-frame-is-an-unrestricted-prompt-injection-and-cost-channel--high) first.
2. **No per-operator identity or audit trail.** Accepted for a single-event deployment.
3. **No session timeout.** Accepted because sessions are attended; it is a live cost risk if that stops being true ([M-01](../review-merged.md#m-01--no-idle-or-absolute-session-timeout-capacity-gate-trivially-exhausted--high)).
4. **Session concurrency cap is per-instance.** The `MaxConcurrentSessions = 2` semaphore is in-process; scale-out multiplies the effective cap. Accepted for single-instance deployments — see [ADR 0005](adr/0005-per-instance-session-cap.md).
5. **No content filtering of avatar output.** Accepted because the model and prompt are controlled and the show is rehearsed.

## Out of scope

Azure platform security, the venue's physical security, and the endpoint security of the operator's laptop.
