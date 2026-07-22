# Voice Live Avatar — Project Specification

**Status:** Ready for implementation handoff
**Owner:** Joran
**Primary audience of this document:** Coding agent and human reviewers
**Last updated:** 2026-07-22

---

## 1. Context and goal

We are building **two independent .NET implementations** of a conversational avatar on **Microsoft Foundry's Voice Live API**:

1. **`/web`** — a website that renders the avatar and gives the user full control over the audio stream. This is the **show client**: it is what runs at a live event.
2. **`/cli`** — a local, voice-only console application. This is the **rehearsal harness**: a fast iteration loop for tuning prompts, grounding content, turn-taking, and voice/session configuration.

**Use case:** the avatar converses **on stage with a C-level leader**, explaining the direction of the company, in front of a live audience. This may happen in a **noisy environment**.

Consequences of that use case, which shape every decision below:

- **Reliability and rehearsability beat features.** Anything that can fail mid-show needs a defined behavior and an operator control.
- The two implementations are **complementary, not competing feature-for-feature**. Do not make them feature-equivalent. The CLI never renders video. The web app is not a tuning tool.
- Both implementations must read the **same external configuration**, so behavior tuned in the CLI transfers directly to the web app.

## 2. Platform facts (verified against current docs)

- Voice Live is accessed over a **WebSocket** carrying JSON events (session config, audio in/out, turn detection, tool calls). The official .NET SDK is the **`Azure.AI.VoiceLive`** NuGet package; console scenarios pair it with `NAudio` for mic capture/playback.
- **Avatar video is a separate plane**: it is streamed over **WebRTC**, negotiated by sending a `session.avatar.connect` event containing the client's SDP offer. Video terminates wherever the peer connection lives — for us, the browser.
- Session options relevant to us: Azure **semantic VAD**, **end-of-utterance detection**, **deep noise suppression**, **echo cancellation**, avatar character/resolution/bitrate config, OpenAI/Azure voices, and **agent mode** (bind the session to a Foundry agent that owns instructions, knowledge, and tools).

## 3. Architecture

### 3.1 Web (`/web`) — the show client

**Stack:** ASP.NET Core (minimal APIs) backend + thin vanilla TypeScript frontend. **No Blazor.** No frontend framework unless genuinely needed; keep the JS surface small.

**Hybrid data flow (the established pattern for browser avatar clients):**

- **Audio + control plane:** Browser → WebSocket → ASP.NET Core backend → WebSocket → Voice Live API. The backend holds credentials, creates the session, applies session config from the config files, and proxies events. Credentials never reach the browser.
- **Video plane:** Browser ↔ Voice Live over a **direct WebRTC connection**. The backend relays the SDP offer/answer (`session.avatar.connect`); the browser's `RTCPeerConnection` attaches the avatar stream to a `<video>` element.

**Two views, both required:**

| | Display view | Operator view |
|---|---|---|
| Audience-facing | Yes | No |
| Content | Fullscreen avatar video only. Zero chrome, no cursor, no debug output, no loading spinners after initial connect. | Device selection, mic gate, turn-taking mode selector, session status, panic controls. |
| Failure behavior | On connection loss: freeze on last frame, never go black. | Show alert, reconnect controls. |

The two views may be two browser windows on one machine, or an operator laptop plus a display machine. Support both; do not assume single-machine.

**Audio control requirements (explicit acceptance criteria — implement each):**

1. Microphone **device selection** (enumerate via `getUserMedia`/`enumerateDevices`).
2. **Mute/unmute** input.
3. **Mic gate** control (see turn-taking policy, §5): operator-clickable and keyboard-bindable (spacebar hold as default push-to-talk binding).
4. **Barge-in** honored or suppressed per the active turn-taking mode. When barge-in triggers: send `response.cancel` and clear the output audio buffer so the avatar stops promptly.
5. Output **volume control and pause** on the operator view.

**Operator panic controls (required):**

- **Stop speaking** — cancel current response immediately.
- **Repeat last answer** — replay/re-ask the last completed response.
- **Safe question** — inject a pre-scripted prompt from config to redirect a derailed conversation.

**Pre-warm requirement:** connecting is an explicit operator action performed before the show. Session creation + avatar WebRTC negotiation takes seconds; the display view must reach a stable idle avatar before anyone walks on stage. Idle state = connected avatar, not a placeholder.

**Reconnect:** automatic reconnect with bounded retries on WebSocket or WebRTC drop. Display view freezes on last frame during reconnect. Operator view surfaces the event loudly.

### 3.2 CLI (`/cli`) — the rehearsal harness

**Stack:** .NET console app. `Azure.AI.VoiceLive` + `Azure.Identity` + `NAudio` + `System.CommandLine` + `Microsoft.Extensions.Configuration` (JSON + environment variables).

**Voice-only. No avatar video, ever.** The session still runs the same model/agent, voice, VAD, and grounding configuration — everything except the video plane. (Stretch goal, explicitly out of scope for v1: spawning a local WebView2/Photino window for video.)

**Purpose-driven features:**

- Talk to the agent through local mic/speakers (device selectable by index/name via flag or config).
- Print a live transcript of both sides to stdout, including turn-detection events (speech start/stop, end-of-utterance) with timestamps — this is the tuning instrumentation.
- Print latency measurements per turn: user-speech-end → first-audio-out.
- Hot-switch: a CLI command (or restart-cheap flags) to reload config between exchanges, so tuning loops are seconds, not minutes.
- Same turn-taking modes as the web app; mic gate bound to a key (default: hold spacebar).

### 3.3 Independence rule

**The two implementations share no code.** They share only:

- The **config schema** (§6) — both validate config at startup against it.
- This spec.

Rationale: this is a two-horse bet; the horses must remain independently deletable, independently buildable, and a change in one must never destabilize the other close to an event. Accept the duplication of session-handling code. Do **not** introduce a shared library.

## 4. Identity, grounding, and tools

- **Auth:** `DefaultAzureCredential` (Entra ID) everywhere. API key path may exist behind a config flag as a documented fallback only.
- **Agent mode is the default:** both clients bind the Voice Live session to a **Foundry agent** (agent ID in config). The agent owns instructions, knowledge (RAG), and tools. Clients stay thin.
- **Grounding pack vs. runtime RAG — implement both paths:**
  - *Grounding pack:* the curated company-direction narrative embedded directly in the agent's instructions. Versioned in `/config/grounding/`. Fast, deterministic — likely the stage default.
  - *Runtime RAG:* agent file-search/knowledge for long-tail questions. Adds latency and nondeterminism per answer.
  - The CLI's latency instrumentation (§3.2) is the tool for measuring the difference during rehearsal. The choice is config, not code.
- **Region:** pinned in config. Choose the nearest avatar-supported region to the venue (from NL: West Europe or Sweden Central). Latency is a feature.

## 5. Turn-taking policy (defined once, tuned in rehearsal)

A named mode in config, honored identically by both implementations:

| Mode | Turn start | Turn end | Barge-in | Intended use |
|---|---|---|---|---|
| `open-mic` | Semantic VAD | End-of-utterance detection | Enabled | Rehearsal experiment; natural feel |
| `gated` | Mic gate opened (key/click) | Mic gate closed | Disabled — avatar always finishes | **Stage default.** Bulletproof in noise. |
| `hybrid` | Mic gate opened | Semantic VAD / EOU | Only while gate is open | Middle ground |

All modes enable **deep noise suppression** and **echo cancellation** in session config.

**Known limitation to document, not solve:** the PA loop. If avatar audio feeds a house PA and the stage mic picks it up, built-in echo cancellation will not fully cope (it targets same-device speaker/mic pairs, not room acoustics). Mitigation is operational: directional mic + `gated` mode. One line in the runbook.

**Audio hardware assumption:** microphone and speaker are connected **directly to the machine running the client** as standard OS audio devices. Anything upstream (XLR interfaces, mixing desks, PA routing) is the AV team's responsibility and out of scope for the software.

## 6. Configuration (external, no recompilation)

All runtime behavior lives in `/config/`, outside both apps, hot-swappable without rebuild:

```
/config/
  avatar.json        # character, customized (bool), resolution, bitrate
  session.json       # voice (type/name/style/temperature), VAD settings,
                     # noise suppression, echo cancellation, region, endpoint
  turntaking.json    # active mode + per-mode parameters (thresholds, timeouts)
  agent.json         # agent ID, grounding strategy (pack | rag | both),
                     # safe-question prompts
  grounding/         # versioned grounding-pack content (markdown)
```

Requirements:

- A **documented JSON schema** for each file lives in `/docs/config-schema.md`.
- Both apps **validate config at startup** and fail fast with a human-readable error naming the file and field. A config typo discovered on stage is a show-stopper; a config typo discovered at launch is a fix.
- Switching avatars = editing `avatar.json` and reconnecting. Nothing more.

## 7. Repository layout

**Hard constraint: no code in the repository root.**

```
/cli/        # .NET console app — self-contained solution
/web/        # ASP.NET Core backend + TS frontend — self-contained solution
/config/     # shared runtime configuration (no code)
/docs/       # this spec, config schema, rehearsal checklist, show runbook
/pipeline/   # CI definitions (build + test both apps independently)
README.md    # orientation only: what this is, links into the folders
```

Each of `/cli` and `/web` builds, tests, and runs on its own with no reference outside its folder except `/config`.

## 8. Show-hardening requirements (first-class, not nice-to-have)

1. **Pre-warm:** operator connects and verifies avatar video before the show. Never a cold connect on stage.
2. **Reconnect:** automatic, bounded retries; display freezes on last frame; operator alerted.
3. **Panic controls:** stop-speaking, repeat-last-answer, safe-question (§3.1).
4. **Network:** wired ethernet or dedicated hotspot; venue Wi-Fi is a documented non-option.
5. **Region pinned** per §4.
6. **Fallback asset:** a pre-recorded video of the avatar answering the three most likely questions lives on the operator machine; the runbook defines when to cut to it. (Producing this video is a rehearsal task, not a code task — the runbook must reference it.)

## 9. Deliverables checklist

- [ ] `/cli` app meeting §3.2, honoring §5 and §6
- [ ] `/web` app meeting §3.1, honoring §5 and §6
- [ ] `/config` populated with working defaults + schema docs in `/docs`
- [ ] `/docs/runbook.md` — show-day procedure: pre-warm steps, panic-control cheatsheet, PA-loop note, network requirements, fallback procedure
- [ ] `/docs/rehearsal-checklist.md` — tuning loop: what to test in the CLI, in which order, and how to carry results into config
- [ ] `/pipeline` building and testing both apps independently

## 10. Out of scope (v1)

- Avatar video in the CLI (stretch: local WebView window)
- Any authentication on the web app itself (localhost/operator-machine deployment assumed — state this in the README)
- Multi-session / multi-avatar concurrency
- Shared code library between the two implementations (deliberately excluded — see §3.3)

## 11. Open items (decide during rehearsal, not before)

- Final turn-taking mode and its thresholds
- Grounding pack vs. runtime RAG (or the split between them)
- Voice selection and style
- Whether `hybrid` mode earns its keep or gets deleted