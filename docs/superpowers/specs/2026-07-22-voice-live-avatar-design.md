# Voice Live Avatar — Design Spec (verified & decided)

**Status:** Ready for implementation planning
**Owner:** Joran (@JoranBergfeld)
**Supersedes:** `docs/initial-spec.md` (kept as history)
**Last updated:** 2026-07-22
**Primary audience:** Coding agent and human reviewers

This document is the implementation-ready evolution of `docs/initial-spec.md`. It folds in
(a) verification of every "platform fact" against the **current** Microsoft Foundry Voice Live
documentation (pages last updated June–July 2026), and (b) seven design decisions taken during
brainstorming. Where this document and `initial-spec.md` disagree, **this document wins**.

---

## 0. Decisions log (this session)

| # | Topic | Decision |
|---|---|---|
| D1 | Region + model | **Sweden Central + native `gpt-realtime`** (speech-to-speech). West Europe rejected: it does **not** offer native realtime models, only cascaded STT→LLM→TTS. |
| D2 | CLI audio I/O | **CLI is Windows-only**, keep **NAudio** (`WaveInEvent`/`WaveOutEvent`/`BufferedWaveProvider`) as in the official samples. CI builds cross-platform; audio rehearsal runs on Windows. |
| D3 | Grounding ownership | **Agent mode for the show**; `/config/grounding/` is the versioned source-of-truth **synced into** the Foundry agent by a `sync-agent` step. CLI additionally supports a **model-mode override** for fast local grounding tuning. |
| D4 | Web credential + media path | **Token broker.** Backend mints short-lived Entra tokens; browser runs `@azure/ai-voicelive` for audio + avatar directly. Replaces the full backend audio proxy. |
| D5 | Reconnect context | **Resume the same conversation via `ConversationId`**; avatar freezes on last frame until reconnected; bounded retries; operator alerted. |
| D6 | "Repeat last answer" | Store the last completed answer transcript and **re-speak it verbatim** via a new response (both apps consistent). |
| D7 | CLI hot-reload | **Smart reload**: live `session.update` for updatable params; auto fast-reconnect only for params that require it. |

---

## 1. Context and goal

We are building **two independent .NET implementations** of a conversational avatar on
**Microsoft Foundry's Voice Live API**:

1. **`/web`** — a website that renders the avatar and gives the user full control over the audio
   stream. This is the **show client**: it runs at a live event.
2. **`/cli`** — a local, voice-only console application. This is the **rehearsal harness**: a fast
   iteration loop for tuning prompts, grounding, turn-taking, and voice/session configuration.

**Use case:** the avatar converses **on stage with a C-level leader**, explaining company
direction, in front of a live audience, possibly in a **noisy environment**.

Consequences that shape every decision below:

- **Reliability and rehearsability beat features.** Anything that can fail mid-show needs a defined
  behavior and an operator control.
- The two implementations are **complementary, not feature-equivalent**. The CLI never renders
  video. The web app is not a tuning tool.
- Both implementations read the **same external configuration** (`/config/`), so behavior tuned in
  the CLI transfers to the web app. **Caveat (D3):** in agent mode the agent owns `instructions`;
  grounding is therefore synced *into* the agent rather than pushed at runtime (see §4).

---

## 2. Platform facts (verified against current docs, 2026-07-22)

All claims below were checked against Microsoft Learn (`learn.microsoft.com/azure/ai-services/speech-service/voice-live*`,
`.../regions`, `.../voice-live-sdk`, `.../voice-live-webrtc`, `.../voice-live-api-reference-2026-06-01-preview`)
and the official samples repo `github.com/microsoft-foundry/voicelive-samples` and
`github.com/Azure/azure-sdk-for-net`.

**Transport & SDK**
- Voice Live is a **WebSocket** API carrying JSON events; it is **compatible with the Azure OpenAI
  Realtime API** event set, plus Azure-specific additions. Server-to-server integration is the
  documented pattern.
- Official **.NET SDK: `Azure.AI.VoiceLive`** — **GA** (latest stable `1.1.0`, 2026-06-04; latest
  overall `1.2.0-beta.1`; SDK source targets service version `2026-07-15`). Key types:
  `VoiceLiveClient`, `VoiceLiveSession`, `VoiceLiveClientOptions`, `VoiceLiveSessionOptions`,
  `SessionUpdate*` event classes, `AzureStandardVoice`, `AzureSemanticVadTurnDetection`,
  `AgentSessionConfig`, `SessionTarget.FromAgent(...)`. Session lifecycle:
  `StartSessionAsync` → `ConfigureSessionAsync` → `GetUpdatesAsync` (+ `AddItemAsync`,
  `StartResponseAsync`, `CancelResponseAsync`).
- Official **JS/TS SDK: `@azure/ai-voicelive`** (`VoiceLiveClient`, `VoiceLiveSession`,
  `session.sendEvent(...)`, `session.subscribe({ onSessionAvatarConnecting, ... })`). This is what
  the web client uses in the browser.
- Console samples pair the SDK with **NAudio** for mic capture/playback (.NET); Python uses PyAudio.

**Endpoint & versioning**
- WebSocket endpoint (new Foundry resources): `wss://<resource>.services.ai.azure.com/voice-live/realtime?api-version=<v>`
  (older resources use `.cognitiveservices.azure.com`). Model mode adds `&model=<model>`; agent mode
  adds agent identifiers (see §4).
- **api-version must be pinned in config.** Docs currently show `2026-04-10`; `azure-realtime` /
  newer features require ≥ `2026-01-01-preview`; the SDK targets `2026-07-15`. We pin explicitly and
  validate at startup.

**Avatar (video plane)** — the spec's original claim is **correct**:
- Avatar video is a **separate WebRTC plane**, negotiated over the WebSocket signaling channel:
  1. Client sends `session.update` with an `avatar` object → server returns **`ice_servers`** inside
     the `session.updated` event.
  2. Client gathers ICE candidates, then sends `session.avatar.connect` with **`client_sdp`**
     (base64-encoded SDP offer).
  3. Server replies `session.avatar.connecting` with **`server_sdp`** (SDP answer).
  4. Client applies the answer; avatar video/audio flow over the peer connection.
- The separate audio-only WebRTC feature (`/voice-live/realtime/calls`, `rtc.call.sdp.create`)
  **cannot host avatar** ("Avatar configurations are currently unsupported with side-band control").
  We therefore use the **WS-signaled avatar path**, not `/calls`.

**Session capabilities (exact field names)**
- Turn detection `turn_detection.type`: `server_vad`, `semantic_vad` (gpt-realtime only),
  **`azure_semantic_vad`**, `azure_semantic_vad_multilingual`. Nested **`end_of_utterance_detection`**
  `{ model: semantic_detection_v1[_multilingual] | smart_end_of_turn_detection, threshold_level, timeout_ms }`.
  Barge-in toggles: `interrupt_response`, `auto_truncate`; plus `threshold`, `prefix_padding_ms`,
  `silence_duration_ms`, `speech_duration_ms`, `remove_filler_words`, `languages`, `create_response`.
- Noise suppression: **`input_audio_noise_reduction.type = azure_deep_noise_suppression`**.
- Echo cancellation: **`input_audio_echo_cancellation.type = server_echo_cancellation`** (removes the
  model's *own* voice from the mic; assumes playback within ~2 s or quality degrades).
- Voice: `voice { type, name, temperature?, rate?, style? }`. Types: `azure-standard`,
  `azure-custom`, `azure-personal`, `azure-realtime-native` (for the `azure-realtime` model),
  and `openai` (`alloy`, `ash`, `ballad`, `coral`, `echo`, `sage`, `shimmer`, `verse`, `marin`, `cedar`).
- Input transcription: `input_audio_transcription.model` (`azure-speech`, `mai-transcribe`,
  `whisper-1`, `gpt-4o-transcribe`, …) — required to surface user-side transcripts.
- Tools/function calling supported (`response.function_call_arguments.delta/done`,
  `conversation.item.create` with `function_call_output`).
- Barge-in cancel: **`response.cancel`** (server → `response.cancelled`).
  **`output_audio_buffer.clear` is avatar-mode only**; non-avatar clients cancel + flush local
  playback.

**Regions (verified 2026-07-09 regions table)**
- Real-time **avatar** is supported in: `westus2, eastus, eastus2, southcentralus, southeastasia,
  centralindia, westeurope, swedencentral, northeurope, italynorth, francecentral`.
- **Native realtime models** (`gpt-realtime`, `gpt-realtime-mini`, `gpt-realtime-1.5`, `azure-realtime`)
  are available in **Sweden Central** but **not West Europe** (WE offers `gpt-4o`/`gpt-4.1`/`gpt-5`
  cascaded only). Both regions support **agent mode**. → **D1: Sweden Central.**

**Auth**
- Recommended: **Microsoft Entra ID** via `DefaultAzureCredential` (`Azure.Identity`). Token scope
  `https://ai.azure.com/.default` (legacy `https://cognitiveservices.azure.com/.default`).
- RBAC: **`Cognitive Services User` + `Foundry User`** (the latter formerly "Azure AI User").
- **Agent mode does not support API-key auth** — Entra only. (This also aligns with tenant ALZ
  policy, which has historically forced `disableLocalAuth=true` on Cognitive Services.)

---

## 3. Architecture

### 3.1 Web (`/web`) — the show client (token-broker architecture, D4)

**Stack:** ASP.NET Core (minimal APIs) backend + thin **vanilla TypeScript** frontend using the
**`@azure/ai-voicelive`** browser SDK. **No Blazor, no SPA framework.**

**Backend responsibilities (thin):**
- **Token broker:** `GET /api/token` → mint a short-lived Entra token via `DefaultAzureCredential`
  (scope `https://ai.azure.com/.default`). Long-lived credentials never leave the server.
- **Config service:** load, validate, and expose the sanitized runtime config (`GET /api/config`)
  from `/config/` — agent identifiers, session/turn-taking/avatar settings, safe-question prompts,
  region, api-version, endpoint.
- **Static hosting** of the built TS/HTML assets.
- No audio proxying and no SDP relay — the browser talks to Voice Live directly.

**Browser responsibilities:**
- Open the Voice Live session in **agent mode** (token from `/api/token`), apply session config from
  `/api/config`, run audio in/out, and negotiate **avatar WebRTC** via `session.avatar.connect` →
  `session.avatar.connecting`, attaching the remote track to a `<video>` element.
- **Token auto-refresh:** refresh before expiry so the pre-warm → show window never lapses.

> **Security note (relaxation of the original rule):** the original spec said "credentials never
> reach the browser." Under D4 this becomes **"only short-lived Entra tokens reach the browser."**
> Acceptable because deployment is a **trusted operator machine with no public exposure** (§10).

**Two views, both required:**

| | Display view | Operator view |
|---|---|---|
| Audience-facing | Yes | No |
| Content | Fullscreen avatar video only. Zero chrome, no cursor, no debug output, no spinners after connect. | Device selection, mic gate, turn-taking mode selector, session status, panic controls. |
| Failure behavior | On connection loss: **freeze on last frame, never go black.** | Show alert, reconnect controls. |

Support **two windows on one machine** or **operator laptop + display machine**. Do not assume
single-machine.

**Audio control requirements (acceptance criteria):**
1. Microphone **device selection** (`getUserMedia`/`enumerateDevices`).
2. **Mute/unmute** input.
3. **Mic gate** control: operator-clickable and keyboard-bindable (spacebar-hold push-to-talk default).
4. **Barge-in** honored/suppressed per active turn-taking mode. When triggered: `response.cancel` +
   `output_audio_buffer.clear` (avatar mode) so the avatar stops promptly.
5. Output **volume control and pause** on the operator view.

**Operator panic controls (required):**
- **Stop speaking** — `response.cancel` immediately.
- **Repeat last answer** — re-speak the stored last-answer transcript verbatim (D6).
- **Safe question** — inject a pre-scripted prompt from config (`conversation.item.create` +
  `response.create`) to redirect a derailed conversation.

**Pre-warm:** connecting is an explicit operator action before the show. The display view must reach
a **stable idle avatar** (connected avatar, not a placeholder) before anyone walks on stage.

**Reconnect:** automatic, bounded retries on WS/WebRTC drop; **resume via `ConversationId`** (D5);
display freezes on last frame; operator alerted loudly.

### 3.2 CLI (`/cli`) — the rehearsal harness

**Stack:** .NET console app, **Windows target** (D2). `Azure.AI.VoiceLive` + `Azure.Identity` +
**NAudio** + `System.CommandLine` + `Microsoft.Extensions.Configuration` (JSON + env vars).

**Voice-only. No avatar video, ever.** Same model/agent, voice, VAD, and grounding config as the web
app minus the video plane. (Stretch, out of scope for v1: local WebView2/Photino video window.)

**Features:**
- Talk to the agent via local mic/speakers (device selectable by index/name via flag or config).
- **Live transcript** of both sides to stdout, including turn-detection events (speech start/stop,
  end-of-utterance) with timestamps — enable `input_audio_transcription` for user-side text; use
  `response.audio_transcript.delta/done` for the agent side.
- **Latency measurements per turn:** user-speech-end (`input_audio_buffer.speech_stopped` / EOU) →
  first `response.audio.delta`.
- **Smart hot-reload (D7):** a CLI command reloads `/config/`; live-updatable params are applied via
  `session.update`; reconnect-only params (agent/model binding, region, api-version) trigger an
  automatic fast reconnect.
- **Model-mode override (D3):** a flag switches the CLI from agent mode to model mode so `instructions`
  (grounding) can be set locally and tuned in seconds; a `sync-agent` subcommand promotes the tuned
  grounding + Voice Live config into the Foundry agent.
- Same turn-taking modes as the web app; mic gate bound to a key (default: hold spacebar).
- Barge-in via `response.cancel` + local playback-buffer flush (no `output_audio_buffer.clear` —
  that is avatar-mode only).

### 3.3 Independence rule

**The two implementations share no code.** They share only:
- The **config schema** (§6) — both validate config at startup against it.
- This spec.

Accept the duplication of session-handling code. Do **not** introduce a shared library. Rationale:
each horse must remain independently deletable, buildable, and stable close to an event.

---

## 4. Identity, grounding, and tools

- **Auth:** `DefaultAzureCredential` (Entra ID) everywhere. Roles: `Cognitive Services User` +
  `Foundry User`; scope `https://ai.azure.com/.default`. **Agent mode is Entra-only.** An API-key
  path may exist **only for the CLI's model-mode dev override**, behind a documented config flag —
  never for the agent-mode show path.
- **Agent mode is the default** for the show. Bind the session to a **Foundry agent** using
  `AgentSessionConfig(agentName, projectName)` / `SessionTarget.FromAgent(...)` (raw WS params are
  `agent-name` + `agent-project-name`; optional `agent-version`, `conversation-id`). The agent owns
  instructions, knowledge (RAG), and tools. **`instructions` cannot be set by the client in agent
  mode.**
- **Grounding (D3):**
  - `/config/grounding/` holds the versioned company-direction narrative (markdown) — the
    **source-of-truth**.
  - A **`sync-agent`** step pushes that grounding into the agent's instructions and pushes the Voice
    Live session config into the agent metadata key `microsoft.voice-live.configuration`.
  - For fast iteration, the **CLI model-mode override** sets `instructions` locally from
    `/config/grounding/` so tuning loops are seconds; the winning text is then promoted via
    `sync-agent`.
  - *Runtime RAG* remains available via the agent's file-search/knowledge for long-tail questions
    (adds latency/nondeterminism). Grounding strategy (`pack | rag | both`) is config, not code.
- **Region:** **Sweden Central** (D1) — nearest avatar-supported region that also offers native
  `gpt-realtime`. Pinned in config. Latency is a feature.

---

## 5. Turn-taking policy (defined once, tuned in rehearsal)

A named mode in config, honored identically (at the UX level) by both implementations. Concrete
mapping to Voice Live session config:

| Mode | Turn start | Turn end | Barge-in | `turn_detection` mapping |
|---|---|---|---|---|
| `open-mic` | Semantic VAD | End-of-utterance | Enabled | `type: azure_semantic_vad`, `end_of_utterance_detection` on, `interrupt_response: true`. Rehearsal/natural feel. |
| `gated` | Mic gate opened (key/click) | Mic gate closed | Disabled — avatar always finishes | Manual turn: append audio only while gate held → `input_audio_buffer.commit` + `response.create`; `interrupt_response: false`. **Stage default.** Bulletproof in noise. |
| `hybrid` | Mic gate opened | Semantic VAD / EOU | Only while gate open | Gate opens streaming; `azure_semantic_vad` + EOU end the turn; `interrupt_response` honored only while the gate is open. |

All modes enable **`azure_deep_noise_suppression`** and **`server_echo_cancellation`**.

> **Wire-level nuance:** barge-in cancellation differs by client — web/avatar uses
> `response.cancel` + `output_audio_buffer.clear`; CLI uses `response.cancel` + local flush. The
> user-observable behavior is identical.

**Known limitation to document, not solve — the PA loop.** If avatar audio feeds a house PA and the
stage mic picks it up, `server_echo_cancellation` (which targets the model's own voice with a ~2 s
playback assumption) will not fully cope with room acoustics. Mitigation is operational: directional
mic + `gated` mode. One line in the runbook.

**Audio hardware assumption:** microphone and speaker are connected **directly to the machine running
the client** as standard OS audio devices. Anything upstream (XLR interfaces, mixing desks, PA
routing) is the AV team's responsibility and out of scope.

---

## 6. Configuration (external, no recompilation)

All runtime behavior lives in `/config/`, outside both apps, hot-swappable without rebuild:

```
/config/
  avatar.json        # web-only: character, style, customized, video{resolution,bitrate,codec,crop,background}
  session.json       # api-version, endpoint, region, model (model-mode), voice, turn_detection,
                     #   end_of_utterance_detection, input_audio_noise_reduction,
                     #   input_audio_echo_cancellation, input_audio_sampling_rate,
                     #   input_audio_transcription
  turntaking.json    # active mode (open-mic|gated|hybrid) + per-mode params (thresholds, timeouts,
                     #   interrupt_response)
  agent.json         # agent-name, agent-project-name, agent-version?, conversation-id policy,
                     #   grounding strategy (pack|rag|both), safe-question prompts
  grounding/         # versioned grounding-pack content (markdown) — source-of-truth, synced to agent
```

**Schema fields align to the verified Voice Live wire format (§2).** Notable additions vs. the
original spec: `api-version` (pinned), `end_of_utterance_detection`, `input_audio_sampling_rate`,
`input_audio_transcription`, `voice.rate`/`voice.style`, and avatar `video.codec`/`video.crop`/
`video.background`.

Requirements:
- A **documented JSON schema** for each file lives in `/docs/config-schema.md`.
- Both apps **validate config at startup** and fail fast with a human-readable error naming the file
  and field. (The CLI ignores `avatar.json`.) A config typo discovered on stage is a show-stopper;
  a typo discovered at launch is a fix.
- Switching avatars = editing `avatar.json` and reconnecting. Nothing more.

---

## 7. Repository layout

**Hard constraint: no code in the repository root.**

```
/cli/        # .NET console app — self-contained solution (Windows target)
/web/        # ASP.NET Core backend + TS frontend — self-contained solution
/config/     # shared runtime configuration (no code)
/tools/      # sync-agent utility (promotes grounding + Voice Live config into the Foundry agent)
/docs/       # specs, config schema, rehearsal checklist, show runbook
/pipeline/   # CI definitions (build + test both apps independently)
README.md    # orientation only
```

Each of `/cli` and `/web` builds, tests, and runs on its own with no reference outside its folder
except `/config`. `/tools/sync-agent` is a standalone utility.

---

## 8. Show-hardening requirements (first-class)

1. **Pre-warm:** operator connects and verifies avatar video before the show. Never a cold connect on
   stage. Idle state = connected avatar.
2. **Reconnect:** automatic, bounded retries; **resume via `ConversationId`**; display freezes on last
   frame; operator alerted. Token auto-refresh keeps the broker token valid across the window.
3. **Panic controls:** stop-speaking, repeat-last-answer (verbatim re-speak, D6), safe-question (§3.1).
4. **Network:** wired ethernet or dedicated hotspot; venue Wi-Fi is a documented non-option.
5. **Region pinned** = Sweden Central (§4).
6. **Fallback asset:** a pre-recorded video of the avatar answering the three most likely questions
   lives on the operator machine; the runbook defines when to cut to it. (Producing this video is a
   rehearsal task, not code.)

---

## 9. Deliverables checklist

- [ ] `/cli` app meeting §3.2, honoring §5 and §6 (Windows + NAudio; model-mode override; smart reload)
- [ ] `/web` token-broker backend + vanilla-TS `@azure/ai-voicelive` frontend meeting §3.1
- [ ] `/tools/sync-agent` promoting grounding + Voice Live config into the Foundry agent (§4)
- [ ] `/config` populated with working defaults + schema docs in `/docs/config-schema.md`
- [ ] `/docs/runbook.md` — pre-warm steps, panic-control cheatsheet, PA-loop note, network
      requirements, fallback procedure
- [ ] `/docs/rehearsal-checklist.md` — tuning loop order and how to carry results into config
- [ ] `/pipeline` building and testing both apps independently

---

## 10. Out of scope (v1)

- Avatar video in the CLI (stretch: local WebView window).
- Any authentication on the web app itself (localhost/operator-machine deployment; state in README).
  Note: the browser holds only a **short-lived Entra token** (D4).
- Multi-session / multi-avatar concurrency.
- Shared code library between the two implementations (deliberately excluded — §3.3).

---

## 11. Open items (decide during rehearsal / confirm during planning)

- Final turn-taking mode and thresholds; whether `hybrid` earns its keep.
- Grounding pack vs. runtime RAG split.
- Voice selection and style (native `azure-realtime-native` voices, e.g. `andrew`/`ava`/`emma`, vs
  Azure HD `azure-standard`).
- **Confirm the exact "verbatim re-speak" mechanism under agent mode** (response-level instruction
  vs. injected conversation item) — flagged during planning, not yet documented by Microsoft.
- Confirm the current agent-mode connect parameter naming for the pinned api-version
  (`agent-name`/`agent-project-name` in 2026-06-01-preview vs `agent_id`/`project_id` in the
  2026-04-10 how-to) — the SDK (`AgentSessionConfig`) abstracts this; verify against the pinned
  version.

---

## Appendix A — Source verification (as of 2026-07-22)

| Claim | Verdict | Source (Microsoft Learn / repos) |
|---|---|---|
| `Azure.AI.VoiceLive` .NET SDK GA + NAudio samples | ✅ | `voice-live-sdk`; `azure-sdk-for-net/samples/voicelive/*` |
| `@azure/ai-voicelive` browser SDK + avatar sample | ✅ | `microsoft-foundry/voicelive-samples/javascript/voice-live-avatar` |
| WebSocket + Realtime-compatible events | ✅ | `voice-live`, `voice-live-how-to` |
| Avatar via `session.avatar.connect {client_sdp}` → `session.avatar.connecting {server_sdp}`; ICE in `session.updated` | ✅ | `voice-live-how-to`, `voice-live-api-reference-2026-06-01-preview` |
| Avatar unsupported on `/realtime/calls` WebRTC-audio | ✅ | `voice-live-webrtc` |
| `azure_semantic_vad`, `end_of_utterance_detection`, `azure_deep_noise_suppression`, `server_echo_cancellation` | ✅ | `voice-live-how-to`, API reference |
| Agent mode = Entra only; `agent-name`+`agent-project-name`; `instructions` blocked | ✅ | `voice-live-agents-quickstart`, `voice-live-how-to` |
| Sweden Central has native realtime + avatar; West Europe avatar-only (no native realtime) | ✅ | `regions?tabs=voice-live`, `regions?tabs=ttsavatar` |
| Roles `Cognitive Services User` + `Foundry User`; scope `ai.azure.com/.default` | ✅ | `voice-live-how-to` |
| api-version churn (`2026-04-10` docs / `2026-07-15` SDK / `azure-realtime` ≥ `2026-01-01-preview`) | ⚠️ pin in config | `voice-live-how-to`, SDK source, `voice-live` overview |
