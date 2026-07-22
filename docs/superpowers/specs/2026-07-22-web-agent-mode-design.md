# Web Agent Mode — Design Spec (verified & decided)

**Status:** Ready for implementation planning
**Owner:** Joran (@JoranBergfeld)
**Last updated:** 2026-07-22
**Primary audience:** Coding agent and human reviewers
**Relates to:** `docs/superpowers/specs/2026-07-22-voice-live-avatar-design.md` (§3.3, §4 agent mode),
`docs/superpowers/plans/2026-07-22-voice-live-avatar-mvp.md` (§7a/§7c)

This spec adds an **agent mode to the `/web` show client** so it connects to the Foundry agent
`company-direction-avatar` instead of a bare realtime model. The point is to let the **agent's
server-side (hosted) tools run** during the show, while keeping the avatar + voice pipeline working
and surfacing tool-invocation events so we can confirm a tool is firing once one is configured.

**Model mode remains the default.** Agent mode is opt-in.

---

## 0. Decisions log (this brainstorm)

| # | Topic | Decision |
|---|---|---|
| W1 | What "use the agent" means | The web starts the Voice Live session via `SessionTarget.FromAgent(...)` so the **agent** owns model + instructions + tools; hosted tools run **server-side** in Foundry. |
| W2 | Tool kind | **Hosted tools only** (web search / knowledge retrieval). No client-side function execution, no MCP execution in this iteration. |
| W3 | Knowledge/RAG now? | **No.** Do not build RAG/indexing. Keep the path **tool-agnostic** so tools added to the agent later work with zero web changes. |
| W4 | "Detect it later" | Build **tool-event observability now**: log + forward the tool/function/MCP events the SDK surfaces, so a later-added tool is visibly confirmable. |
| W5 | Mode selection | **Config field `mode` in `session.json`** (default `model`), with **env var `VOICELIVE_MODE` override**. |
| W6 | Avatar/voice source in agent mode | **Primary:** drive voice + avatar from our `/config` via `session.update` (a web `BuildForAgent`) + `ConnectAvatarAsync`. Gated by a live spike (§4). **Fallback** if rejected: put voice+avatar into the agent's voice-live metadata (deferred sync-agent step). |
| W7 | Default behavior | Model mode stays default; the verified model-mode + avatar path is untouched. |

---

## 1. Context and goal

Today the `/web` bridge always starts a **model-mode** session:

- `web/src/VoiceLive.Web/Session/VoiceLiveWebSocketBridge.cs:33` →
  `client.StartSessionAsync(config.Model, ...)` then
  `ConfigureSessionAsync(SessionOptionsBuilder.Build(config, "<hardcoded assistant instructions>"))`.
- It *reads* `config/agent.json` but only echoes `agentName` + `safeQuestions` to the browser as UI
  labels — the session never connects to the agent.

The `/cli` already has a **verified** agent-mode path we mirror:

- `cli/src/VoiceLive.Cli/Run/LiveSessionRunner.cs:97` →
  `StartSessionAsync(SessionTarget.FromAgent(new AgentSessionConfig(agentName, project)))`.
- `cli/src/VoiceLive.Cli/Session/SessionOptionsBuilder.cs` → `BuildForAgent(config)` **omits
  `Model` + `Instructions`** (agent owns them) and keeps voice / turn detection / audio format /
  sampling rate / modalities. Sending `Instructions` in agent mode is rejected by the service with
  `instructions_configuration_not_supported` (verified live this session).

**Goal:** give the `/web` show client the same agent-mode capability, so the agent — and any hosted
tools it is configured with — drive the conversation, without losing avatar/voice and with
tool-invocation events made observable.

---

## 2. Verified platform facts (grounded 2026-07-22, testlab-f / proj-default)

All facts below were checked live this session against `testlab-f`
(`https://testlab-f.services.ai.azure.com`, project `proj-default`) or by reflecting the SDK
(`Azure.AI.VoiceLive` **1.1.0**, `net10.0`). **Nothing here is assumed.**

- **`ConnectAvatarAsync` is a `VoiceLiveSession` method**, i.e. session-level and independent of how
  the session was started (model vs `FromAgent`). `SessionTarget.FromAgent` exists. → Avatar is
  **API-compatible** with agent mode; runtime acceptance of avatar via `session.update` is the one
  thing still to verify (§4).
- **Tool/function events the SDK surfaces** (our observability hooks):
  `SessionUpdateResponseFunctionCallArgumentsDelta`, `SessionUpdateResponseFunctionCallArgumentsDone`,
  `SessionUpdateMcpListToolsInProgress/Completed/Failed`.
- **Hosted-tool caveat (explicit, not assumed):** purely server-side hosted tools (web search,
  Azure AI Search) may **not** emit the client-facing `FunctionCall*` events — those are the
  client-executed function-calling protocol. Whether a hosted-tool invocation produces any discrete
  client event is **unverified** and is recorded by the spike (§4). If none is emitted, provable
  detection later needs an MCP/function tool (which do emit events) or server-side logs.
- **Project connections** (`GET {project}/connections?api-version=v1`): `testlab-f-userowned`
  (AzureStorageAccount) and `ragpipee9fbadsearchj9kdyn` (**CognitiveSearch** →
  `ragpipe-e9fbad-search`). **There is no Grounding-with-Bing connection**, so a hosted `web_search`
  tool is **not available** without provisioning one. Azure AI Search *is* available as a future
  knowledge backing — out of scope here (W3).
- **Agent `company-direction-avatar`** current version (v2): `kind=prompt`, `model=gpt-5`,
  `instructions="placeholder2"`, **`tools=[]`**, metadata keys `microsoft.voice-live.enabled`,
  `probe`. It carries **no tools and no real voice-live voice/avatar metadata yet**. Configuring it
  is out of scope for this iteration (W3) except as the §4 fallback.
- **Web session options already set the avatar**: `SessionOptionsBuilder.Build`
  (`web/src/VoiceLive.Web/Session/SessionOptionsBuilder.cs`) sets `Avatar = BuildAvatar(config.Avatar)`.
  So the agent-mode builder is `Build` minus `Model` + `Instructions`, **keeping** `Avatar`.

---

## 3. Design

### 3.1 Mode selection (W5)

- Add optional field `"mode": "model" | "agent"` to `config/session.json`; **default `"model"`** when
  absent.
- Env var **`VOICELIVE_MODE`** (`model` | `agent`) **overrides** the config field when set.
- Resolution happens **once** at startup in `web/src/VoiceLive.Web/Program.cs`, producing a single
  effective `SessionMode` value carried on `ServerSessionConfig` (new `Mode` member).
- **Validation:** any value other than `model`/`agent` (from either source) throws a
  `WebConfigValidationException` with a clear message — fail fast, don't default silently.
- The effective mode is added to the browser `ready` message `config` block (alongside the existing
  `activeMode`, `agentName`, `safeQuestions`) so the operator UI can show which mode is live.

### 3.2 Bridge wiring (W1)

Add `SessionOptionsBuilder.BuildForAgent(ServerSessionConfig config)` to the **web** builder
(mirrors the CLI). It is exactly `Build` **without** `Model` and `Instructions`, keeping:
`Voice`, `TurnDetection`, `InputAudioFormat`/`OutputAudioFormat` (`Pcm16`), `InputAudioSamplingRate`,
**`Avatar`**, `InputAudioNoiseReduction`, `InputAudioEchoCancellation`, `InputAudioTranscription`,
and the `Text`+`Audio` modalities. (Factor the shared body so `Build` and `BuildForAgent` cannot
drift.)

`VoiceLiveWebSocketBridge.RunAsync` branches on `config.Mode`:

- **model** (unchanged):
  `session = await client.StartSessionAsync(config.Model, ct);`
  `await session.ConfigureSessionAsync(SessionOptionsBuilder.Build(config, instructions), ct);`
- **agent:**
  `var agent = new AgentSessionConfig(config.Agent.AgentName, config.Agent.AgentProjectName);`
  `session = await client.StartSessionAsync(SessionTarget.FromAgent(agent), ct);`
  `await session.ConfigureSessionAsync(SessionOptionsBuilder.BuildForAgent(config), ct);`

Everything downstream — avatar SDP encode/`ConnectAvatarAsync`, ICE server extraction, browser pumps,
transcript/`response.done`, error handling — is **reused unchanged**.

### 3.3 Avatar-in-agent-mode spike (W6) — gating, front-loaded

The load-bearing unknown: does agent mode accept a `session.update` that includes `Avatar` (and
voice/turn/audio), and does `ConnectAvatarAsync` then succeed? The CLI proved voice/turn/audio are
accepted in agent mode; **avatar via `session.update` is the untested addition.**

The plan's **first executable task** is a live spike against `testlab-f` +
`company-direction-avatar`:

1. Start agent-mode session, `ConfigureSessionAsync(BuildForAgent(config))` (with avatar).
2. Expect `SessionUpdateSessionUpdated` carrying `Session.Avatar.IceServers`, then a successful
   `ConnectAvatarAsync` → `SessionUpdateAvatarConnecting` with a server SDP answer.

Outcomes:

- **Accepted** → primary path is valid; **no agent metadata needed**. Proceed with the design as-is.
- **Rejected** (e.g. an `avatar`-equivalent of `instructions_configuration_not_supported`) → **stop
  and report**. Fallback requires voice+avatar to live in the **agent's voice-live metadata**
  (`microsoft.voice-live.configuration` chunked keys), which is the deferred `sync-agent` work — we
  bring that decision back to the owner rather than silently expanding scope.

The spike result (including exactly which tool/avatar events were observed) is recorded in the plan
and, if it changes platform understanding, stored as a repository memory.

### 3.4 Tool-event observability (W4)

In `PumpVoiceLiveUpdatesAsync`, add cases for the events from §2:

- `SessionUpdateResponseFunctionCallArgumentsDelta` / `...Done`
- `SessionUpdateMcpListToolsInProgress` / `...Completed` / `...Failed`

For each: **log** at `Information` (tool/call id, name, truncated args), and **forward** a compact
message to the browser: `{ "t": "tool", "phase": "start|args|done|list", "name": <string?>,
"callId": <string?> }`. The operator view (`web/frontend/src/views.ts` / `main.ts`) renders a small,
non-intrusive "tool: `<name>`" note. No behavior depends on tools — this is purely diagnostic.

Because of the §2 hosted-tool caveat, the observability is **best-effort**: it surfaces whatever the
SDK emits. Its real value is confirmed by the spike; documented honestly either way.

---

## 4. Config, schema, and docs changes

- `config/session.json`: add `"mode": "model"` (explicit default; no behavior change).
- `docs/config-schema.md`: document `session.json.mode` and the `VOICELIVE_MODE` override + precedence.
- `web/README.md`: one paragraph on flipping to agent mode and what it does (connects to the agent;
  hosted tools run server-side).

---

## 5. Testing

- **Unit (web):**
  - `BuildForAgent` sets no `Model`/`Instructions` and **keeps** `Voice`, `Avatar`, turn detection,
    audio formats, sampling rate, modalities (mirror of the CLI `BuildForAgent` test).
  - Mode resolution: default = `model`; `session.json.mode=agent` → `agent`; `VOICELIVE_MODE`
    overrides the config field; invalid value (either source) → `WebConfigValidationException`.
  - `ready`-message config includes `mode`.
- **Live spike (manual/scripted):** §3.3 — the gating avatar-agent test. Then a full avatar-agent
  E2E via the existing Playwright harness (`/tmp/e2e/run.mjs`) against `company-direction-avatar`.
- **Regression:** existing model-mode unit tests and the model-mode avatar E2E stay green.

---

## 6. Failure modes (explicit + graceful — per owner preference)

- **Unknown mode value** (config or env) → fail fast at startup with a clear config error.
- **Agent not found / not authorized** → surface the Azure error verbatim to the operator via the
  existing `SendErrorAndCloseAsync` path; **do not** mask it as a normal message.
- **Avatar rejected in agent mode** → explicit operator banner + logged reason; only fall back to the
  metadata path if/when we implement it (owner decision).
- **Tool events absent for hosted tools** → not an error; documented as a known limitation, with the
  MCP/function-tool or server-log alternatives noted.

---

## 7. Non-goals (YAGNI)

- No RAG / knowledge indexing; no Azure AI Search wiring.
- No changes to the agent's definition/tools/metadata this iteration (except the §3.3 fallback, which
  is a separate owner-gated decision).
- No client-side function execution or MCP execution.
- No change to the browser↔server audio/avatar wire protocol.
- No new `web_search` connection provisioning.

---

## 8. Open risks / must-verify (no fabrication)

1. **Avatar via `session.update` in agent mode** (§3.3) — gating spike; primary path depends on it.
2. **Hosted-tool event emission** (§2, §3.4) — spike records what is actually observable; the design
   does not assume hosted tools emit client events.
