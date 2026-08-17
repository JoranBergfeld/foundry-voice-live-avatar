var __getOwnPropNames = Object.getOwnPropertyNames;
var __esm = (fn, res, err) => function __init() {
  if (err) throw err[0];
  try {
    return fn && (res = (0, fn[__getOwnPropNames(fn)[0]])(fn = 0)), res;
  } catch (e) {
    throw err = [e], e;
  }
};
var __commonJS = (cb, mod) => function __require() {
  try {
    return mod || (0, cb[__getOwnPropNames(cb)[0]])((mod = { exports: {} }).exports, mod), mod.exports;
  } catch (e) {
    throw mod = 0, e;
  }
};

// src/views.ts
function button(label) {
  const element = document.createElement("button");
  element.type = "button";
  element.textContent = label;
  element.disabled = true;
  return element;
}
function statusLine(label) {
  const line = document.createElement("p");
  line.className = "status-line";
  line.dataset.label = label;
  line.textContent = `${label}: pending`;
  return line;
}
function setText(element, value) {
  element.textContent = value;
}
function nextWordBoundary(text, from) {
  let index = from;
  while (index < text.length && text[index] === " ") index += 1;
  while (index < text.length && text[index] !== " ") index += 1;
  return index;
}
function revealDelayForWord(wordLength) {
  return Math.min(REVEAL_MAX_MS, Math.max(REVEAL_MIN_MS, REVEAL_BASE_MS + REVEAL_PER_CHAR_MS * wordLength));
}
function createSubtitleController(toggle, overlay) {
  let enabled = false;
  let fullText = "";
  let revealed = 0;
  let started = false;
  let complete = false;
  let awaitingNewTurn = false;
  let revealTimer;
  let holdTimer;
  let fadeClearTimer;
  const cancelRevealTimer = () => {
    if (revealTimer !== void 0) clearTimeout(revealTimer);
    revealTimer = void 0;
  };
  const cancelFadeTimers = () => {
    if (holdTimer !== void 0) clearTimeout(holdTimer);
    if (fadeClearTimer !== void 0) clearTimeout(fadeClearTimer);
    holdTimer = void 0;
    fadeClearTimer = void 0;
  };
  const resetState = () => {
    fullText = "";
    revealed = 0;
    started = false;
    complete = false;
    awaitingNewTurn = false;
  };
  const clearPresentation = () => {
    resetState();
    overlay.textContent = "";
    overlay.classList.remove("fading");
    overlay.hidden = true;
  };
  const beginHoldFade = () => {
    if (holdTimer !== void 0 || fadeClearTimer !== void 0) return;
    awaitingNewTurn = true;
    holdTimer = setTimeout(() => {
      holdTimer = void 0;
      overlay.classList.add("fading");
      fadeClearTimer = setTimeout(() => {
        fadeClearTimer = void 0;
        clearPresentation();
      }, FADE_MS);
    }, HOLD_MS);
  };
  const showRevealed = () => {
    overlay.textContent = fullText.slice(0, revealed);
    overlay.classList.remove("fading");
    overlay.hidden = false;
  };
  const pump = () => {
    cancelRevealTimer();
    if (!enabled || !started) return;
    if (revealed >= fullText.length) {
      if (complete) beginHoldFade();
      return;
    }
    const boundary = nextWordBoundary(fullText, revealed);
    const wordLength = fullText.slice(revealed, boundary).trim().length;
    revealTimer = setTimeout(() => {
      revealTimer = void 0;
      revealed = boundary;
      showRevealed();
      pump();
    }, revealDelayForWord(wordLength));
  };
  const startNewTurnIfNeeded = () => {
    if (!awaitingNewTurn) return;
    cancelRevealTimer();
    cancelFadeTimers();
    fullText = "";
    revealed = 0;
    started = false;
    complete = false;
    awaitingNewTurn = false;
    overlay.textContent = "";
    overlay.classList.remove("fading");
    overlay.hidden = true;
  };
  toggle.setAttribute("aria-pressed", "false");
  toggle.onclick = () => {
    enabled = !enabled;
    toggle.setAttribute("aria-pressed", String(enabled));
    if (!enabled) {
      cancelRevealTimer();
      cancelFadeTimers();
      clearPresentation();
    }
  };
  return {
    setAgentSubtitle(text, final) {
      if (!enabled || !text) return;
      startNewTurnIfNeeded();
      cancelFadeTimers();
      fullText = final ? text : fullText + text;
      pump();
    },
    noteAgentSpeaking() {
      startNewTurnIfNeeded();
      cancelFadeTimers();
      started = true;
      if (revealTimer === void 0 && revealed < fullText.length) {
        revealed = nextWordBoundary(fullText, revealed);
        showRevealed();
      }
      pump();
    },
    noteAgentIdle() {
    },
    completeAgentSubtitle() {
      complete = true;
      if (!enabled || overlay.hidden && fullText.length === 0) {
        resetState();
        return;
      }
      started = true;
      pump();
    },
    clearAgentSubtitle() {
      cancelRevealTimer();
      cancelFadeTimers();
      clearPresentation();
    }
  };
}
function createTranscriptAppender(list) {
  const liveText = { user: "", agent: "" };
  return function addTranscript(role, text, final) {
    const existing = list.querySelector(`.transcript-line.live.${role}`);
    const line = existing ?? document.createElement("p");
    const transcriptText = final ? text : liveText[role] + text;
    liveText[role] = final ? "" : transcriptText;
    line.className = `transcript-line ${role} ${final ? "final" : "live"}`;
    line.textContent = `${role === "user" ? "You" : "Agent"}${final ? "" : " (live)"}: ${transcriptText}`;
    if (!existing) list.append(line);
    if (final) line.classList.remove("live");
    line.scrollIntoView({ block: "nearest" });
  };
}
function renderOperatorView(root) {
  document.body.classList.remove("display-view", "landing-view");
  root.replaceChildren();
  const shell = document.createElement("main");
  shell.className = "operator-shell";
  const heading = document.createElement("h1");
  heading.textContent = "Voice Live Operator";
  const error = document.createElement("div");
  error.className = "error-banner";
  error.hidden = true;
  error.setAttribute("role", "alert");
  const reconnectButton = document.createElement("button");
  reconnectButton.type = "button";
  reconnectButton.className = "reconnect-button";
  reconnectButton.textContent = "Reconnect";
  reconnectButton.hidden = true;
  const avatarPanel = document.createElement("section");
  avatarPanel.className = "avatar-panel";
  const avatar = document.createElement("video");
  avatar.id = "avatar";
  avatar.autoplay = true;
  avatar.playsInline = true;
  avatarPanel.append(avatar);
  const configPanel = document.createElement("section");
  configPanel.className = "config-panel";
  const agentLine = document.createElement("p");
  agentLine.textContent = "Agent: waiting for server";
  const sessionModeLine = document.createElement("p");
  sessionModeLine.textContent = "Session mode: waiting for server";
  const modeLine = document.createElement("p");
  modeLine.textContent = "Turn-taking: waiting for server";
  const avatarLine = document.createElement("p");
  avatarLine.textContent = "Avatar: waiting for server";
  configPanel.append(agentLine, sessionModeLine, modeLine, avatarLine);
  const statuses = /* @__PURE__ */ new Map();
  const statusPanel = document.createElement("section");
  statusPanel.className = "status-panel";
  for (const name of ["connection", "webrtc", "microphone", "turn", "speech", "avatar"]) {
    const line = statusLine(name);
    statuses.set(name, line);
    statusPanel.append(line);
  }
  const controls = document.createElement("section");
  controls.className = "controls";
  const holdButton = button("Hold to talk");
  const stopButton = button("Stop speaking");
  const repeatButton = button("Repeat last answer");
  const safeQuestionPanel = document.createElement("div");
  safeQuestionPanel.className = "safe-questions";
  controls.append(holdButton, stopButton, repeatButton, safeQuestionPanel);
  const transcriptPanel = document.createElement("section");
  transcriptPanel.className = "transcripts";
  const transcriptHeading = document.createElement("h2");
  transcriptHeading.textContent = "Transcript";
  const transcriptList = document.createElement("div");
  transcriptList.className = "transcript-list";
  transcriptPanel.append(transcriptHeading, transcriptList);
  const addTranscript = createTranscriptAppender(transcriptList);
  const safeQuestionButtons = [];
  const toolsPanel = document.createElement("section");
  toolsPanel.className = "tools-panel";
  const toolsHeading = document.createElement("h2");
  toolsHeading.textContent = "Tool activity";
  const toolsList = document.createElement("div");
  toolsList.className = "tools-list";
  const toolsEmpty = document.createElement("p");
  toolsEmpty.className = "tools-empty";
  toolsEmpty.textContent = "No tool calls yet.";
  toolsPanel.append(toolsHeading, toolsList, toolsEmpty);
  toolsPanel.hidden = true;
  const nonFatal = document.createElement("div");
  nonFatal.className = "nonfatal-notice";
  nonFatal.hidden = true;
  nonFatal.setAttribute("role", "status");
  shell.append(heading, error, reconnectButton, nonFatal, avatarPanel, configPanel, statusPanel, controls, transcriptPanel, toolsPanel);
  root.append(shell);
  return {
    root,
    avatar,
    holdButton,
    stopButton,
    repeatButton,
    safeQuestionButtons,
    setConfig(config) {
      setText(agentLine, `Agent: ${config.agentName}`);
      setText(sessionModeLine, `Session mode: ${config.mode ?? "model"}`);
      setText(modeLine, `Turn-taking: ${config.activeMode}`);
      setText(avatarLine, `Avatar: ${config.avatarCharacter ?? "configured"}${config.avatarStyle ? ` (${config.avatarStyle})` : ""}`);
      toolsPanel.hidden = config.mode !== "agent";
      safeQuestionPanel.replaceChildren();
      safeQuestionButtons.splice(0);
      for (const question of config.safeQuestions) {
        const safeButton = button(question);
        safeQuestionButtons.push(safeButton);
        safeQuestionPanel.append(safeButton);
      }
    },
    setStatus(name, value) {
      const line = statuses.get(name);
      if (line) line.textContent = `${name}: ${value}`;
    },
    setError(message) {
      error.hidden = false;
      error.textContent = message;
    },
    clearError() {
      error.hidden = true;
      error.textContent = "";
    },
    setReady(ready) {
      holdButton.disabled = !ready;
      stopButton.disabled = !ready;
      repeatButton.disabled = !ready;
      for (const safeButton of safeQuestionButtons) safeButton.disabled = !ready;
    },
    setReconnectHandler(handler) {
      reconnectButton.onclick = handler;
    },
    setDisconnected(disconnected) {
      reconnectButton.hidden = !disconnected;
      holdButton.disabled = disconnected;
      stopButton.disabled = disconnected;
      repeatButton.disabled = disconnected;
      for (const safeButton of safeQuestionButtons) safeButton.disabled = disconnected;
    },
    setHoldActive(active) {
      holdButton.classList.toggle("active", active);
      holdButton.textContent = active ? "Release to end turn" : "Hold to talk";
    },
    addTranscript,
    noteTool(text) {
      toolsEmpty.hidden = true;
      const line = document.createElement("p");
      line.className = "tool-line";
      const stamp = (/* @__PURE__ */ new Date()).toLocaleTimeString();
      line.textContent = `${stamp} \u2014 ${text}`;
      toolsList.append(line);
      while (toolsList.childElementCount > 8) toolsList.firstElementChild?.remove();
      line.scrollIntoView({ block: "nearest" });
    },
    noteNonFatal(message) {
      nonFatal.hidden = false;
      nonFatal.textContent = message;
    }
  };
}
function renderLandingView(root) {
  document.body.classList.add("landing-view");
  document.body.classList.remove("display-view");
  root.replaceChildren();
  const avatar = document.createElement("video");
  avatar.id = "avatar";
  avatar.className = "landing-avatar";
  avatar.autoplay = true;
  avatar.playsInline = true;
  const pill = document.createElement("div");
  pill.className = "landing-pill";
  pill.hidden = true;
  const actions = document.createElement("nav");
  actions.className = "landing-actions";
  actions.setAttribute("aria-label", "Landing controls");
  const gear = document.createElement("a");
  gear.className = "landing-action landing-config";
  gear.href = "?view=operator";
  gear.textContent = "\u2699 Config";
  gear.setAttribute("aria-label", "Config");
  gear.title = "Config";
  const holdButton = document.createElement("button");
  holdButton.type = "button";
  holdButton.className = "landing-talk";
  holdButton.textContent = "\u{1F3A4} Hold to talk";
  holdButton.disabled = true;
  holdButton.hidden = true;
  const transcriptToggle = document.createElement("button");
  transcriptToggle.type = "button";
  transcriptToggle.className = "landing-action landing-transcript-toggle";
  transcriptToggle.textContent = "\u{1F4AC} Transcript";
  transcriptToggle.setAttribute("aria-label", "Transcript");
  transcriptToggle.title = "Transcript";
  const subtitleToggle = document.createElement("button");
  subtitleToggle.type = "button";
  subtitleToggle.className = "landing-action subtitle-toggle";
  subtitleToggle.textContent = "Subtitles";
  subtitleToggle.setAttribute("aria-label", "Subtitles");
  const subtitleOverlay = document.createElement("div");
  subtitleOverlay.className = "live-subtitle landing-subtitle";
  subtitleOverlay.hidden = true;
  subtitleOverlay.setAttribute("aria-hidden", "true");
  const panel = document.createElement("aside");
  panel.className = "landing-transcript";
  const panelHeader = document.createElement("header");
  const panelTitle = document.createElement("span");
  panelTitle.textContent = "Transcript";
  const panelClose = document.createElement("button");
  panelClose.type = "button";
  panelClose.className = "landing-transcript-close";
  panelClose.textContent = "\xD7";
  panelClose.setAttribute("aria-label", "Close panel");
  panelHeader.append(panelTitle, panelClose);
  const transcriptList = document.createElement("div");
  transcriptList.className = "landing-transcript-list";
  panel.append(panelHeader, transcriptList);
  const togglePanel = () => panel.classList.toggle("open");
  transcriptToggle.onclick = togglePanel;
  panelClose.onclick = () => panel.classList.remove("open");
  const notice = document.createElement("div");
  notice.className = "landing-notice";
  notice.hidden = true;
  notice.setAttribute("role", "status");
  const errorOverlay = document.createElement("div");
  errorOverlay.className = "landing-error";
  errorOverlay.hidden = true;
  errorOverlay.setAttribute("role", "alert");
  const reconnectButton = document.createElement("button");
  reconnectButton.type = "button";
  reconnectButton.className = "reconnect-button landing-reconnect";
  reconnectButton.textContent = "Reconnect";
  reconnectButton.hidden = true;
  actions.append(gear, transcriptToggle, subtitleToggle);
  root.append(avatar, pill, actions, holdButton, panel, notice, errorOverlay, reconnectButton, subtitleOverlay);
  const addTranscript = createTranscriptAppender(transcriptList);
  const subtitles = createSubtitleController(subtitleToggle, subtitleOverlay);
  return {
    ...subtitles,
    root,
    avatar,
    holdButton,
    supportsMuteToggle: true,
    setConfig() {
    },
    setStatus(name, value) {
      if (name !== "connection" && name !== "webrtc") return;
      if (name === "webrtc" && value === "connected") {
        pill.hidden = true;
        return;
      }
      pill.hidden = false;
      pill.textContent = value;
    },
    setError(message) {
      subtitles.clearAgentSubtitle();
      errorOverlay.hidden = false;
      errorOverlay.textContent = message;
    },
    clearError() {
      errorOverlay.hidden = true;
      errorOverlay.textContent = "";
    },
    setReady(ready) {
      holdButton.disabled = !ready;
    },
    setReconnectHandler(handler) {
      reconnectButton.onclick = handler;
    },
    setDisconnected(disconnected) {
      if (disconnected) subtitles.clearAgentSubtitle();
      reconnectButton.hidden = !disconnected;
      holdButton.disabled = disconnected;
    },
    setHoldActive(active) {
      holdButton.classList.toggle("active", active);
      holdButton.textContent = active ? "\u{1F3A4} Release to end turn" : "\u{1F3A4} Hold to talk";
    },
    setMuted(muted) {
      holdButton.classList.toggle("muted", muted);
      holdButton.textContent = muted ? "\u{1F507} Muted \u2014 tap to unmute" : "\u{1F3A4} Listening \u2014 tap to mute";
    },
    addTranscript,
    noteNonFatal(message) {
      notice.hidden = false;
      notice.textContent = message;
    }
  };
}
function renderDisplayView(root) {
  document.body.classList.add("display-view");
  document.body.classList.remove("landing-view");
  root.replaceChildren();
  const video = document.createElement("video");
  video.id = "avatar";
  video.autoplay = true;
  video.playsInline = true;
  const overlay = document.createElement("div");
  overlay.className = "display-status";
  overlay.setAttribute("role", "status");
  const message = document.createElement("span");
  message.textContent = "Connecting to avatar session\u2026";
  const reconnectButton = document.createElement("button");
  reconnectButton.type = "button";
  reconnectButton.className = "reconnect-button display-reconnect";
  reconnectButton.textContent = "Reconnect";
  reconnectButton.hidden = true;
  const subtitleToggle = document.createElement("button");
  subtitleToggle.type = "button";
  subtitleToggle.className = "subtitle-toggle display-subtitle-toggle";
  subtitleToggle.textContent = "Subtitles";
  subtitleToggle.setAttribute("aria-label", "Subtitles");
  const subtitleOverlay = document.createElement("div");
  subtitleOverlay.className = "live-subtitle display-subtitle";
  subtitleOverlay.hidden = true;
  subtitleOverlay.setAttribute("aria-hidden", "true");
  overlay.append(message, reconnectButton);
  root.append(video, overlay, subtitleToggle, subtitleOverlay);
  const subtitles = createSubtitleController(subtitleToggle, subtitleOverlay);
  return {
    ...subtitles,
    root,
    avatar: video,
    setStatus(value) {
      overlay.classList.remove("error");
      overlay.setAttribute("role", "status");
      message.textContent = value;
    },
    setError(value) {
      subtitles.clearAgentSubtitle();
      overlay.classList.add("error");
      overlay.setAttribute("role", "alert");
      message.textContent = value;
    },
    clearError() {
      overlay.classList.remove("error");
      overlay.setAttribute("role", "status");
      message.textContent = "";
    },
    setReconnectHandler(handler) {
      reconnectButton.onclick = handler;
    },
    setDisconnected(disconnected) {
      if (disconnected) subtitles.clearAgentSubtitle();
      reconnectButton.hidden = !disconnected;
    }
  };
}
var REVEAL_BASE_MS, REVEAL_PER_CHAR_MS, REVEAL_MIN_MS, REVEAL_MAX_MS, HOLD_MS, FADE_MS;
var init_views = __esm({
  "src/views.ts"() {
    "use strict";
    REVEAL_BASE_MS = 80;
    REVEAL_PER_CHAR_MS = 38;
    REVEAL_MIN_MS = 150;
    REVEAL_MAX_MS = 460;
    HOLD_MS = 2600;
    FADE_MS = 300;
  }
});

// src/main.ts
var require_main = __commonJS({
  "src/main.ts"() {
    init_views();
    var wsUrl = `${location.protocol === "https:" ? "wss" : "ws"}://${location.host}/ws/session`;
    function isInteractiveView(view) {
      return "holdButton" in view;
    }
    function isSubtitleView(view) {
      return "setAgentSubtitle" in view;
    }
    function parseServerFrame(data) {
      const frame = JSON.parse(data);
      if (typeof frame.t !== "string") throw new Error("server frame missing t");
      return frame;
    }
    function waitForIceGatheringComplete(pc) {
      if (pc.iceGatheringState === "complete") return Promise.resolve();
      return new Promise((resolve) => {
        const timeout = window.setTimeout(done, 2500);
        function done() {
          window.clearTimeout(timeout);
          pc.removeEventListener("icegatheringstatechange", onChange);
          resolve();
        }
        function onChange() {
          if (pc.iceGatheringState === "complete") done();
        }
        pc.addEventListener("icegatheringstatechange", onChange);
      });
    }
    var ThinVoiceLiveClient = class {
      view;
      interactive;
      socket;
      pc;
      audioContext;
      audioNodes = [];
      micStream;
      streamingMic = false;
      gatedHoldIntent;
      readyConfig;
      pingId = 0;
      sessionToken = 0;
      disconnected = true;
      disconnectPromise;
      constructor(view) {
        this.view = view;
        this.interactive = isInteractiveView(view) ? view : void 0;
        this.view.setReconnectHandler(() => this.start());
      }
      start() {
        if (this.socket || this.disconnectPromise) return;
        this.disconnected = false;
        const token = ++this.sessionToken;
        this.view.setDisconnected(false);
        this.interactive?.setReady(false);
        this.view.clearError();
        this.setStatus("connection", "connecting");
        let socket;
        try {
          socket = new WebSocket(wsUrl);
        } catch (error) {
          void this.disconnect(`WebSocket setup failed: ${error instanceof Error ? error.message : String(error)}`, token);
          return;
        }
        this.socket = socket;
        socket.binaryType = "arraybuffer";
        socket.addEventListener("open", () => {
          if (this.isCurrentSession(token, socket)) this.setStatus("connection", "connected; waiting for ready");
        });
        socket.addEventListener("message", (event) => {
          if (this.isCurrentSession(token, socket)) void this.onMessage(event, token);
        });
        socket.addEventListener("error", () => {
          if (!this.isCurrentSession(token, socket)) return;
          void this.disconnect("WebSocket failed. Check that the ASP.NET app is running and /ws/session is available.", token);
        });
        socket.addEventListener("close", (event) => {
          if (!this.isCurrentSession(token, socket)) return;
          const message = event.wasClean ? void 0 : "WebSocket closed unexpectedly; the server-side Voice Live session ended.";
          void this.disconnect(message, token);
        });
        this.pingId = window.setInterval(() => {
          if (this.isCurrentSession(token, socket)) this.send({ t: "ping" });
        }, 25e3);
      }
      isCurrentSession(token, socket) {
        return token === this.sessionToken && (!socket || this.socket === socket);
      }
      async onMessage(event, token) {
        if (!this.isCurrentSession(token)) return;
        if (typeof event.data !== "string") return;
        let frame;
        try {
          frame = parseServerFrame(event.data);
        } catch (error) {
          this.fail(`Could not parse server message: ${error instanceof Error ? error.message : String(error)}`);
          return;
        }
        switch (frame.t) {
          case "ready":
            await this.onReady(frame, token);
            break;
          case "avatar-answer":
            await this.onAvatarAnswer(frame.sdp, token);
            break;
          case "user-transcript":
            this.interactive?.addTranscript("user", frame.text, frame.final);
            break;
          case "agent-transcript":
            this.interactive?.addTranscript("agent", frame.text, frame.final);
            if (isSubtitleView(this.view)) this.view.setAgentSubtitle(frame.text, frame.final);
            break;
          case "speech-started":
            this.setStatus("speech", "started");
            break;
          case "speech-stopped":
            this.setStatus("speech", "stopped");
            break;
          case "avatar-speaking":
            this.setStatus("avatar", "speaking");
            if (isSubtitleView(this.view)) this.view.noteAgentSpeaking();
            break;
          case "avatar-idle":
            this.setStatus("avatar", "idle");
            if (isSubtitleView(this.view)) this.view.noteAgentIdle();
            break;
          case "response-done":
            this.setStatus("turn", "response done");
            if (isSubtitleView(this.view)) this.view.completeAgentSubtitle();
            break;
          case "tool": {
            const label = frame.name ? `${frame.phase}: ${frame.name}` : frame.phase;
            const idSuffix = frame.callId ? ` (${frame.callId})` : "";
            this.interactive?.noteTool?.(`tool ${label}${idSuffix}`);
            break;
          }
          case "avatar-error":
            this.handleAvatarError(frame.message);
            break;
          case "error":
            this.fail(`Server error: ${frame.message}`);
            break;
        }
      }
      async onReady(frame, token) {
        if (!this.isCurrentSession(token)) return;
        this.readyConfig = frame.config;
        if (this.interactive) {
          this.interactive.clearError();
          this.interactive.setConfig(frame.config);
          this.interactive.setReady(true);
          this.wireInteractiveControls(frame.config);
        } else {
          this.view.setStatus(`Ready: ${frame.config.agentName}`);
        }
        this.setStatus("connection", "ready");
        await this.negotiateAvatar(frame.iceServers, token);
        if (!this.isCurrentSession(token)) return;
        if (this.interactive) await this.prepareMicrophone(frame.config.activeMode, token);
      }
      wireInteractiveControls(config) {
        const view = this.interactive;
        if (!view) return;
        const gated = config.activeMode === "gated";
        this.gatedHoldIntent = void 0;
        view.holdButton.onclick = null;
        view.holdButton.onpointerdown = null;
        view.holdButton.onpointerup = null;
        view.holdButton.onpointerleave = null;
        view.holdButton.onpointercancel = null;
        if (gated) {
          view.holdButton.hidden = false;
          view.holdButton.onpointerdown = (event) => {
            event.preventDefault();
            this.startGatedTurn();
          };
          const endGated = () => this.endGatedTurn();
          view.holdButton.onpointerup = endGated;
          view.holdButton.onpointerleave = endGated;
          view.holdButton.onpointercancel = endGated;
        } else if (view.supportsMuteToggle) {
          view.holdButton.hidden = false;
          view.holdButton.onclick = () => this.toggleMute();
        } else {
          view.holdButton.hidden = true;
        }
        if (view.stopButton) {
          view.stopButton.onclick = () => {
            this.send({ t: "barge-in" });
            this.setStatus("turn", "barge-in sent");
          };
        }
        if (view.repeatButton) {
          view.repeatButton.onclick = () => this.sendSay("Please repeat your previous answer.");
        }
        if (view.safeQuestionButtons) {
          for (const safeButton of view.safeQuestionButtons) {
            safeButton.onclick = () => this.sendSay(safeButton.textContent ?? "");
          }
        }
      }
      async negotiateAvatar(iceServers, token) {
        this.setStatus("webrtc", "creating peer connection");
        try {
          const pc = new RTCPeerConnection({
            iceServers: iceServers.map((server) => ({
              urls: server.urls,
              username: server.username,
              credential: server.credential
            }))
          });
          if (!this.isCurrentSession(token)) {
            pc.close();
            return;
          }
          this.pc = pc;
          pc.addTransceiver("video", { direction: "recvonly" });
          pc.addTransceiver("audio", { direction: "recvonly" });
          pc.ontrack = (event) => {
            if (!this.isCurrentSession(token) || this.pc !== pc) return;
            const [stream] = event.streams;
            if (!stream) return;
            if (this.view.avatar.srcObject === stream) return;
            this.view.avatar.srcObject = stream;
            this.view.avatar.play().catch((error) => {
              if (error instanceof DOMException && error.name === "AbortError") return;
              void this.disconnect(
                `Browser blocked avatar playback: ${error instanceof Error ? error.message : String(error)}. Interact with the page and retry if needed.`,
                token
              );
            });
          };
          pc.onconnectionstatechange = () => {
            if (this.isCurrentSession(token) && this.pc === pc) this.setStatus("webrtc", pc.connectionState);
          };
          const offer = await pc.createOffer();
          if (!this.isCurrentSession(token) || this.pc !== pc) return;
          await pc.setLocalDescription(offer);
          await waitForIceGatheringComplete(pc);
          if (!this.isCurrentSession(token) || this.pc !== pc) return;
          const sdp = pc.localDescription?.sdp;
          if (!sdp) throw new Error("browser did not create a local SDP offer");
          this.send({ t: "avatar-offer", sdp });
          this.setStatus("webrtc", "offer sent; waiting for answer");
        } catch (error) {
          await this.disconnect(`Avatar WebRTC negotiation failed: ${error instanceof Error ? error.message : String(error)}`, token);
        }
      }
      async onAvatarAnswer(sdp, token) {
        if (!this.isCurrentSession(token)) return;
        if (!this.pc) {
          await this.disconnect("Received avatar SDP answer before the browser peer connection existed.", token);
          return;
        }
        const pc = this.pc;
        try {
          await pc.setRemoteDescription({ type: "answer", sdp });
          if (!this.isCurrentSession(token) || this.pc !== pc) return;
          this.setStatus("webrtc", "answer applied");
        } catch (error) {
          await this.disconnect(`Browser rejected avatar SDP answer: ${error instanceof Error ? error.message : String(error)}`, token);
        }
      }
      async prepareMicrophone(activeMode, token) {
        if (!this.interactive) return;
        try {
          this.setStatus("microphone", "requesting permission");
          const micStream = await navigator.mediaDevices.getUserMedia({
            audio: { channelCount: 1, echoCancellation: true, noiseSuppression: true }
          });
          if (!this.isCurrentSession(token)) {
            micStream.getTracks().forEach((track) => track.stop());
            return;
          }
          this.micStream = micStream;
          const audioContext = new AudioContext({ sampleRate: 24e3 });
          this.audioContext = audioContext;
          await audioContext.audioWorklet.addModule("/pcm-worklet.js");
          if (!this.isCurrentSession(token) || this.audioContext !== audioContext) return;
          const source = audioContext.createMediaStreamSource(micStream);
          const worklet = new AudioWorkletNode(audioContext, "pcm16-worklet");
          const silentOutput = audioContext.createGain();
          silentOutput.gain.value = 0;
          worklet.port.onmessage = (event) => {
            if (this.isCurrentSession(token) && this.streamingMic && this.socket?.readyState === WebSocket.OPEN) this.socket.send(event.data);
          };
          source.connect(worklet).connect(silentOutput).connect(audioContext.destination);
          this.audioNodes = [source, worklet, silentOutput];
          this.setStatus("microphone", `ready (${Math.round(audioContext.sampleRate)} Hz context)`);
          if (activeMode === "open-mic" || activeMode === "hybrid") {
            await audioContext.resume();
            if (!this.isCurrentSession(token) || this.audioContext !== audioContext) return;
            this.streamingMic = true;
            this.interactive?.setMuted?.(false);
            this.setStatus("turn", `${activeMode}: streaming continuously`);
          } else {
            this.setStatus("turn", "gated: hold to talk");
          }
        } catch (error) {
          await this.disconnect(`Microphone setup failed: ${error instanceof Error ? error.message : String(error)}`, token);
        }
      }
      async startGatedTurn() {
        if (!this.interactive) return;
        const token = this.sessionToken;
        const audioContext = this.audioContext;
        if (!audioContext) {
          this.fail("Microphone is not ready; cannot start a gated turn.");
          return;
        }
        const holdIntent = { token, audioContext };
        this.gatedHoldIntent = holdIntent;
        try {
          await audioContext.resume();
        } catch (error) {
          if (!this.isCurrentSession(token) || this.audioContext !== audioContext || this.gatedHoldIntent !== holdIntent) return;
          this.gatedHoldIntent = void 0;
          this.fail(`Could not resume microphone capture: ${error instanceof Error ? error.message : String(error)}`);
          return;
        }
        if (!this.isCurrentSession(token) || this.audioContext !== audioContext || this.gatedHoldIntent !== holdIntent) return;
        this.send({ t: "start-turn" });
        this.streamingMic = true;
        this.interactive.setHoldActive(true);
        this.setStatus("turn", "recording gated turn");
      }
      endGatedTurn() {
        this.gatedHoldIntent = void 0;
        if (!this.streamingMic) return;
        this.stopMicStreaming();
        this.interactive?.setHoldActive(false);
        this.send({ t: "end-turn" });
        this.setStatus("turn", "gated turn sent");
      }
      stopMicStreaming() {
        this.streamingMic = false;
      }
      toggleMute() {
        this.streamingMic = !this.streamingMic;
        this.interactive?.setMuted?.(!this.streamingMic);
        this.setStatus("microphone", this.streamingMic ? "live" : "muted");
      }
      sendSay(text) {
        if (text.trim().length === 0) return;
        this.send({ t: "say", text });
        this.setStatus("turn", "say sent");
      }
      send(frame) {
        if (this.socket?.readyState === WebSocket.OPEN) this.socket.send(JSON.stringify(frame));
      }
      setStatus(name, value) {
        if (this.interactive) this.interactive.setStatus(name, value);
        else if (name === "connection" || name === "webrtc" || name === "avatar") this.view.setStatus(`${name}: ${value}`);
      }
      handleAvatarError(message) {
        this.pc?.close();
        this.pc = void 0;
        this.setStatus("avatar", "unavailable");
        this.setStatus("webrtc", "avatar disabled (capacity)");
        if (this.interactive) this.interactive.noteNonFatal(`Avatar unavailable: ${message}`);
        else this.view.setStatus(`Avatar unavailable: ${message}`);
      }
      fail(message) {
        if (isSubtitleView(this.view)) this.view.clearAgentSubtitle();
        if (this.interactive) this.interactive.setError(message);
        else this.view.setError(message);
      }
      async disconnect(message, token) {
        if (token !== void 0 && token !== this.sessionToken) return;
        if (this.disconnectPromise) {
          await this.disconnectPromise;
          return;
        }
        if (this.disconnected) return;
        this.disconnected = true;
        ++this.sessionToken;
        this.gatedHoldIntent = void 0;
        this.interactive?.setReady(false);
        this.interactive?.setHoldActive(false);
        if (isSubtitleView(this.view)) this.view.clearAgentSubtitle();
        const cleanup = (async () => {
          if (this.pingId) {
            window.clearInterval(this.pingId);
            this.pingId = 0;
          }
          this.stopMicStreaming();
          const micStream = this.micStream;
          this.micStream = void 0;
          micStream?.getTracks().forEach((track) => {
            try {
              track.stop();
            } catch {
            }
          });
          const audioNodes = this.audioNodes;
          this.audioNodes = [];
          for (const node of audioNodes) {
            try {
              node.disconnect();
            } catch {
            }
          }
          const audioContext = this.audioContext;
          this.audioContext = void 0;
          const pc = this.pc;
          this.pc = void 0;
          try {
            pc?.close();
          } catch {
          }
          this.view.avatar.srcObject = null;
          const socket = this.socket;
          this.socket = void 0;
          if (socket && socket.readyState < WebSocket.CLOSING) {
            try {
              socket.close();
            } catch {
            }
          }
          this.readyConfig = void 0;
          if (audioContext && audioContext.state !== "closed") {
            try {
              await audioContext.close();
            } catch {
            }
          }
          this.setStatus("connection", "disconnected");
          if (message) this.fail(message);
          this.view.setDisconnected(true);
        })();
        this.disconnectPromise = cleanup;
        try {
          await cleanup;
        } finally {
          if (this.disconnectPromise === cleanup) this.disconnectPromise = void 0;
        }
      }
      dispose() {
        void this.disconnect();
      }
    };
    function boot() {
      const viewName = new URLSearchParams(location.search).get("view") ?? "landing";
      const root = document.getElementById("app");
      if (!root) throw new Error("Missing #app root element.");
      const view = viewName === "operator" ? renderOperatorView(root) : viewName === "display" ? renderDisplayView(root) : renderLandingView(root);
      const client = new ThinVoiceLiveClient(view);
      window.addEventListener("beforeunload", () => client.dispose());
      client.start();
    }
    try {
      boot();
    } catch (error) {
      document.body.innerHTML = `<pre style="color:red">Startup failed: ${error instanceof Error ? error.message : String(error)}</pre>`;
    }
  }
});
export default require_main();
