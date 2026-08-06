# Production deployment

**Scope.** How to run this application for a real event with real stakes. [`runbook.md`](runbook.md) covers provisioning and rehearsal; [`rehearsal-checklist.md`](rehearsal-checklist.md) covers the hours before showtime. This document covers everything between: identity, secrets, capacity, cost, observability, environments, rollback and disaster recovery.

**Read [Production readiness](../README.md#production-readiness) first.** The gate list there is a hard prerequisite for this document.

## 1. Identity model

**What ships:** a single shared username and password, validated by custom middleware, issuing an **8-hour sliding cookie**. Everyone who signs in is the same principal.

**What that means:** there is no per-operator identity, no audit trail attributable to a person, no way to revoke one operator's access without changing the password for everyone, and no authorization model — every authenticated user can reach every endpoint, including `say`, which makes the avatar speak arbitrary text on stage.

**Acceptable when:** the app is on a trusted network, the audience of the credential is a named handful of people, and the event is attended.

**Not acceptable when:** the app is internet-reachable, the credential is shared beyond the event team, or the deployment outlives the event.

**The standard remedy** is App Service Easy Auth with Microsoft Entra ID, which removes credential custody from the application entirely and gives per-operator identity and revocation. It is not implemented here. If you need it, treat it as a prerequisite project, not a deployment-time toggle.

## 2. Secrets

**Never** set the operator password with `azd env set AUTH_PASSWORD <password>` for a production deployment. That lands the sole credential in plaintext App Service configuration, readable by anyone with Contributor or Website Contributor on the resource (any principal holding `Microsoft.Web/sites/config/list/action`).

Use a Key Vault reference instead:

```bash
az keyvault secret set --vault-name <vault> --name auth-password --value "<password>"

az webapp config appsettings set --name <app> --resource-group <rg> --settings \
  "Auth__Password=@Microsoft.KeyVault(VaultName=<vault>;SecretName=auth-password)"
```

The web app's system-assigned managed identity needs **Key Vault Secrets User** on the vault. Verify resolution before the event — a failed reference surfaces as the literal `@Microsoft.KeyVault(...)` string becoming the password, so **sign in successfully after every secret change**.

**Rotation.** Rotate after every event and whenever anyone with the credential leaves the team. Rotation is a secret update. Session cookies are validated by ASP.NET Core Data Protection keys, not by re-checking the stored credentials: **a password or username change alone does not revoke any active session**. The cookie is a self-contained Data Protection payload, and the 8-hour expiry is **sliding** — an active tab renews the cookie on each request and never ages out on its own.

**There is no immediate revocation mechanism in the application.** On App Service the Data Protection key ring is stored under `%HOME%/ASP.NET/DataProtection-Keys`, which persists across restarts and scale operations (App Service rule 1: keys are persisted when the app is hosted in Azure App Service). To force all sessions to drop you must destroy the key ring — delete `%HOME%/ASP.NET/DataProtection-Keys` via Kudu/SSH then restart, or configure an external key store (Azure Blob or Key Vault) that can be purged — after which every existing cookie becomes invalid.

**Key Vault reference ordering.** The `Auth__Password` application setting is also written on every `azd provision`, sourced from the `AUTH_PASSWORD` environment variable. Any `azd up` or `azd provision` run after you set the Key Vault reference will overwrite it. Re-apply the Key Vault reference and re-verify sign-in after every provision. For a first deployment, `AUTH_PASSWORD` must be set to a non-empty value so the app starts with a valid credential; replace it with the Key Vault reference immediately after provision.

**Never** commit credentials. `appsettings.Development.json` carries no `Auth` section, and a test enforces that.

## 3. Capacity and quota

Three independent limits, in the order you will hit them:

| Limit | Value | Behaviour when exceeded | Where to change |
|---|---|---|---|
| Concurrent app sessions | `MaxConcurrentSessions`, default **2** | New connections are rejected at the gate | `VoiceLive__MaxConcurrentSessions` App Service application setting (overrides the `appsettings.json` default) |
| Avatar rendering quota | Per Azure AI Foundry resource | `avatar_service_resource_exhausted`; the peer connection closes and **both audio and video are lost — there is no voice-only fallback** | Azure quota request |
| App Service instance | B1, single instance | CPU saturation and dropped audio | App Service plan |

**The concurrency gate is per-instance and in-memory.** Scaling out to N instances does not share the cap — it multiplies it to N × `MaxConcurrentSessions`, silently. **Do not scale out to increase capacity.** Scale up instead, and raise `MaxConcurrentSessions` deliberately, having tested the instance can carry the load.

**Each browser tab is a session.** An operator view plus a display view is two sessions — the entire default budget. Plan the slot count against the number of tabs you will actually open, plus one spare for a mid-show reconnect.

**Request avatar quota before the event, not on the day.** Quota approval is not instant, and the failure mode is a media-plane failure — the peer connection closes and the avatar goes dark, while the WebSocket session, microphone and transcripts keep running.

## 4. Cost

Voice Live bills **per session-minute**, and **there is no session timeout in this application** (finding M-01). A forgotten browser tab holds a session open and bills until the tab closes, the app restarts, or the socket drops.

**Guardrails, in order of effectiveness:**

1. Close every tab at the end of the event. This is the only control that exists today; put it on the teardown checklist.
2. Stop the App Service between rehearsal and event day. Sessions cannot outlive the process.
3. Set a budget alert on the resource group so an overrun is noticed in hours, not on the invoice.
4. Implement M-01 (absolute + idle timeouts) if this deployment will run unattended at any point.

Cost drivers, largest first: avatar rendering minutes, realtime model audio minutes, then App Service compute, then Application Insights ingestion. The fixed infrastructure cost is trivial next to a session left open over a weekend.

## 5. Observability

Application Insights and Log Analytics are provisioned, and the app emits OpenTelemetry metrics — but **no alert exists until you create one.** Provisioned telemetry that nobody watches is not observability.

**Minimum alert set before an event:**

| Alert | Signal | Why |
|---|---|---|
| Health degraded | `/api/health` availability test, non-200 for 2 consecutive minutes | Catches invalid config and lost RBAC before showtime |
| Session start failures | Exception rate on the session-start path > 0 over 5 minutes | The `403`/`429`/quota failures that end a show |
| Capacity rejections | `voicelive.active_sessions` sustained at `MaxConcurrentSessions` | Someone opened one tab too many (gate-rejection metric is not yet emitted — M-01 remediation) |
| Instance health | CPU > 80% for 5 minutes | B1 saturation drops audio |

Useful Log Analytics queries:

```kusto
// Failed session starts in the last hour, by reason
AppExceptions
| where TimeGenerated > ago(1h)
| summarize count() by ProblemId, bin(TimeGenerated, 5m)
| order by TimeGenerated desc

// Health endpoint status over the last 6 hours
AppRequests
| where TimeGenerated > ago(6h) and Url endswith "/api/health"
| summarize count() by ResultCode, bin(TimeGenerated, 15m)

// Avatar quota exhaustion — both audio and video are lost when this fires
AppTraces
| where TimeGenerated > ago(24h) and Message has "avatar_service_resource_exhausted"
| project TimeGenerated, Message
```

**Suggested SLO for an event window:** 100% availability of `/api/health` and zero failed session starts during the show, measured over the rehearsal-to-teardown window rather than a rolling month. An event either works or it does not; a monthly error budget is the wrong instrument.

**Diagnostic settings are not configured by default** (finding L-04). Route App Service and Foundry resource logs to the Log Analytics workspace before the event, or post-incident analysis will have nothing to read.

## 6. Environments and deployment

There is **no CD pipeline**. CI builds and tests but never deploys and never runs `dotnet publish`, so the artifact-producing path is not exercised by automation (finding L-17). Deployment is a manual `azd up`.

**Minimum viable environment model:**

| Environment | Purpose | Provisioning |
|---|---|---|
| `dev` | Local, config from `config/`, credentials from user-secrets | `dotnet run` |
| rehearsal | Full Azure deployment, same region and SKU as production, used for the rehearsal checklist | `azd up` with its own `azd` environment |
| event | The deployment the show runs on | `azd up` with its own `azd` environment |

Use **separate `azd` environments**, not a shared one — a shared environment means the rehearsal deploy and the event deploy are the same resources, so any rehearsal change is a production change.

**Deploy at least 24 hours before the event**, then freeze. Run the full [rehearsal checklist](rehearsal-checklist.md) against the frozen deployment.

## 7. Rollback

The most important production procedure, and the fastest.

```bash
# List recent deployments, newest first
az webapp log deployment list --name <app> --resource-group <rg> \
  --query "[].{id:id, time:received_time, active:active}" -o table

# Roll back by re-deploying a retained artifact (rollback is re-deploying a previous build;
# the deployment id is for correlation and audit only — no slot-swap rollback is available
# on the B1 plan, which cannot host staging slots)
az webapp deploy --name <app> --resource-group <rg> --src-path <previous.zip> --type zip
```

**Prepare a rollback before the event:** keep the last known-good published artifact, and record its deployment id in the event runbook. Mid-show is not when you discover the artifact is gone.

**Configuration rollback is separate.** Config is read from `config/` **at startup only** and there is no hot reload (finding L-20). Changing config requires an app restart, which drops every live session. **Never edit config during a show.** Treat `config/` changes as deployments: change, restart, re-verify `/api/health`, re-run the smoke test.

## 8. Business continuity

The whole project exists to serve one high-stakes live moment, so plan for the region being degraded 30 minutes before it.

| Scenario | Prepared fallback |
|---|---|
| Foundry region degraded | Pre-provision a second `azd` environment in an alternate region **that supports native realtime voice, avatar and agent mode**. Verify it during rehearsal — an untested standby is not a standby. |
| Avatar quota exhausted | `handleAvatarError` closes the peer connection; **both audio and video are lost**. This is a media-plane failure, not a full session failure — the WebSocket session, microphone and transcripts keep running. Prepare a full fallback plan (pre-recorded segment or static slides), agree the abort call, and brief the speaker beforehand so the failure is not a surprise on stage. |
| App Service unreachable | Have the pre-recorded segment or static slides ready. Agree the abort call and who makes it. |
| Network loss in the venue | The media plane is direct browser↔Azure WebRTC; there is no offline mode. Venue connectivity is a single point of failure — test it from the actual stage position, on the actual network, during rehearsal. |

Write the abort decision into the event runbook: **who** calls it, **when**, and **what** replaces the segment.

## 9. Networking

Default `azd up` produces a public endpoint with the App Service default hostname.

- **Custom domain and TLS** — bind a custom domain with an App Service managed certificate if the URL is visible to the audience.
- **Access restrictions** — the highest-value single hardening step. Restrict inbound access to the venue's egress IP range:

  ```bash
  az webapp config access-restriction add --name <app> --resource-group <rg> \
    --rule-name venue --action Allow --ip-address <venue-cidr> --priority 100
  ```

  This converts "one shared password on the public internet" into "one shared password on a network you control", which is the assumption the whole design rests on.
- **Private endpoints / VNet integration** are not configured (finding L-03). Consider them if the app must reach Foundry over a private path.
- **`AllowedHosts` is `*`** (finding M-07). Set it to the actual hostname.

## 10. Data handling and privacy

**Applies to every deployment. Confirm before an event with real attendees.**

- **Microphone audio** is streamed to Azure Foundry Voice Live for the duration of a turn. This application does not write audio to disk and does not persist it.
- **Transcripts** are relayed to the browser for display and are not persisted server-side. They exist in browser memory until the tab closes.
- **Conversations are not stored.** There is no history, no cross-session memory and no user profile.
- **Application Insights** captures request telemetry and exceptions. Confirm no transcript content reaches log messages before deploying anywhere with real attendee speech.
- **Azure-side retention** is governed by your Foundry resource configuration, not by this application. Review the abuse-monitoring and data-retention settings on the Foundry resource and, if required, apply for the limited-access exemption from human review.
- **Region** is pinned to `swedencentral`, keeping processing in the EU. Changing the region changes where speech is processed — a compliance decision, not just a latency one. See [ADR 0006](adr/0006-region-pinned-swedencentral.md).

**Tell the audience.** If audience speech can reach the microphone, that is a recording notice obligation in most jurisdictions.
