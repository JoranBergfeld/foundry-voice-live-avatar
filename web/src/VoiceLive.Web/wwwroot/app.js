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
function renderOperatorView(root) {
  document.body.classList.remove("display-view");
  root.replaceChildren();
  const shell = document.createElement("main");
  shell.className = "operator-shell";
  const heading = document.createElement("h1");
  heading.textContent = "Voice Live Operator";
  const error = document.createElement("div");
  error.className = "error-banner";
  error.hidden = true;
  error.setAttribute("role", "alert");
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
  const modeLine = document.createElement("p");
  modeLine.textContent = "Turn-taking: waiting for server";
  const avatarLine = document.createElement("p");
  avatarLine.textContent = "Avatar: waiting for server";
  configPanel.append(agentLine, modeLine, avatarLine);
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
  const safeQuestionButtons = [];
  const liveText = { user: "", agent: "" };
  shell.append(heading, error, avatarPanel, configPanel, statusPanel, controls, transcriptPanel);
  root.append(shell);
  function addTranscript(role, text, final) {
    const existing = transcriptList.querySelector(`.transcript-line.live.${role}`);
    const line = existing ?? document.createElement("p");
    const transcriptText = final ? text : liveText[role] + text;
    if (final) liveText[role] = "";
    else liveText[role] = transcriptText;
    line.className = `transcript-line ${role} ${final ? "final" : "live"}`;
    line.textContent = `${role === "user" ? "You" : "Agent"}${final ? "" : " (live)"}: ${transcriptText}`;
    if (!existing) transcriptList.append(line);
    if (final) line.classList.remove("live");
    line.scrollIntoView({ block: "nearest" });
  }
  return {
    root,
    avatar,
    holdButton,
    stopButton,
    repeatButton,
    safeQuestionButtons,
    setConfig(config) {
      setText(agentLine, `Agent: ${config.agentName}`);
      setText(modeLine, `Turn-taking: ${config.activeMode}`);
      setText(avatarLine, `Avatar: ${config.avatarCharacter ?? "configured"}${config.avatarStyle ? ` (${config.avatarStyle})` : ""}`);
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
    setHoldActive(active) {
      holdButton.classList.toggle("active", active);
      holdButton.textContent = active ? "Release to end turn" : "Hold to talk";
    },
    addTranscript
  };
}
function renderDisplayView(root) {
  document.body.classList.add("display-view");
  root.replaceChildren();
  const video = document.createElement("video");
  video.id = "avatar";
  video.autoplay = true;
  video.playsInline = true;
  const overlay = document.createElement("div");
  overlay.className = "display-status";
  overlay.textContent = "Connecting to avatar session\u2026";
  root.append(video, overlay);
  return {
    root,
    avatar: video,
    setStatus(message) {
      overlay.classList.remove("error");
      overlay.textContent = message;
    },
    setError(message) {
      overlay.classList.add("error");
      overlay.textContent = message;
    }
  };
}
var init_views = __esm({
  "src/views.ts"() {
    "use strict";
  }
});

// src/main.ts
var require_main = __commonJS({
  "src/main.ts"() {
    init_views();
    var wsUrl = `${location.protocol === "https:" ? "wss" : "ws"}://${location.host}/ws/session`;
    function isOperatorView(view) {
      return "holdButton" in view;
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
      operator;
      socket;
      pc;
      audioContext;
      audioNodes = [];
      micStream;
      streamingMic = false;
      readyConfig;
      pingId = 0;
      constructor(view) {
        this.view = view;
        this.operator = isOperatorView(view) ? view : void 0;
      }
      start() {
        this.setStatus("connection", "connecting");
        this.socket = new WebSocket(wsUrl);
        this.socket.binaryType = "arraybuffer";
        this.socket.addEventListener("open", () => this.setStatus("connection", "connected; waiting for ready"));
        this.socket.addEventListener("message", (event) => void this.onMessage(event));
        this.socket.addEventListener("error", () => this.fail("WebSocket failed. Check that the ASP.NET app is running and /ws/session is available."));
        this.socket.addEventListener("close", (event) => {
          this.stopMicStreaming();
          this.setStatus("connection", `closed${event.code ? ` (${event.code})` : ""}`);
          if (!event.wasClean) this.fail("WebSocket closed unexpectedly; the server-side Voice Live session ended.");
        });
        this.pingId = window.setInterval(() => this.send({ t: "ping" }), 25e3);
      }
      async onMessage(event) {
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
            await this.onReady(frame);
            break;
          case "avatar-answer":
            await this.onAvatarAnswer(frame.sdp);
            break;
          case "user-transcript":
          case "agent-transcript":
            this.operator?.addTranscript(frame.t === "user-transcript" ? "user" : "agent", frame.text, frame.final);
            break;
          case "speech-started":
            this.setStatus("speech", "started");
            break;
          case "speech-stopped":
            this.setStatus("speech", "stopped");
            break;
          case "avatar-speaking":
            this.setStatus("avatar", "speaking");
            break;
          case "avatar-idle":
            this.setStatus("avatar", "idle");
            break;
          case "response-done":
            this.setStatus("turn", "response done");
            break;
          case "error":
            this.fail(`Server error: ${frame.message}`);
            break;
        }
      }
      async onReady(frame) {
        this.readyConfig = frame.config;
        if (this.operator) {
          this.operator.clearError();
          this.operator.setConfig(frame.config);
          this.operator.setReady(true);
          this.wireOperatorControls(frame.config);
        } else {
          this.view.setStatus(`Ready: ${frame.config.agentName}`);
        }
        this.setStatus("connection", "ready");
        await this.negotiateAvatar(frame.iceServers);
        if (this.operator) await this.prepareMicrophone(frame.config.activeMode);
      }
      wireOperatorControls(config) {
        if (!this.operator) return;
        const gated = config.activeMode === "gated";
        this.operator.holdButton.hidden = !gated;
        this.operator.holdButton.onpointerdown = (event) => {
          event.preventDefault();
          if (!gated) return;
          this.startGatedTurn();
        };
        const endGated = () => {
          if (gated) this.endGatedTurn();
        };
        this.operator.holdButton.onpointerup = endGated;
        this.operator.holdButton.onpointerleave = endGated;
        this.operator.holdButton.onpointercancel = endGated;
        this.operator.stopButton.onclick = () => {
          this.send({ t: "barge-in" });
          this.setStatus("turn", "barge-in sent");
        };
        this.operator.repeatButton.onclick = () => this.sendSay("Please repeat your previous answer.");
        for (const safeButton of this.operator.safeQuestionButtons) {
          safeButton.onclick = () => this.sendSay(safeButton.textContent ?? "");
        }
      }
      async negotiateAvatar(iceServers) {
        this.setStatus("webrtc", "creating peer connection");
        try {
          this.pc = new RTCPeerConnection({
            iceServers: iceServers.map((server) => ({
              urls: server.urls,
              username: server.username,
              credential: server.credential
            }))
          });
          this.pc.addTransceiver("video", { direction: "recvonly" });
          this.pc.addTransceiver("audio", { direction: "recvonly" });
          this.pc.ontrack = (event) => {
            const [stream] = event.streams;
            if (!stream) return;
            if (this.view.avatar.srcObject === stream) return;
            this.view.avatar.srcObject = stream;
            this.view.avatar.play().catch((error) => {
              if (error instanceof DOMException && error.name === "AbortError") return;
              this.fail(`Browser blocked avatar playback: ${error instanceof Error ? error.message : String(error)}. Interact with the page and retry if needed.`);
            });
          };
          this.pc.onconnectionstatechange = () => this.setStatus("webrtc", this.pc?.connectionState ?? "unknown");
          const offer = await this.pc.createOffer();
          await this.pc.setLocalDescription(offer);
          await waitForIceGatheringComplete(this.pc);
          const sdp = this.pc.localDescription?.sdp;
          if (!sdp) throw new Error("browser did not create a local SDP offer");
          this.send({ t: "avatar-offer", sdp });
          this.setStatus("webrtc", "offer sent; waiting for answer");
        } catch (error) {
          this.fail(`Avatar WebRTC negotiation failed: ${error instanceof Error ? error.message : String(error)}`);
        }
      }
      async onAvatarAnswer(sdp) {
        if (!this.pc) {
          this.fail("Received avatar SDP answer before the browser peer connection existed.");
          return;
        }
        try {
          await this.pc.setRemoteDescription({ type: "answer", sdp });
          this.setStatus("webrtc", "answer applied");
        } catch (error) {
          this.fail(`Browser rejected avatar SDP answer: ${error instanceof Error ? error.message : String(error)}`);
        }
      }
      async prepareMicrophone(activeMode) {
        if (!this.operator) return;
        try {
          this.setStatus("microphone", "requesting permission");
          this.micStream = await navigator.mediaDevices.getUserMedia({
            audio: { channelCount: 1, echoCancellation: true, noiseSuppression: true }
          });
          this.audioContext = new AudioContext({ sampleRate: 24e3 });
          await this.audioContext.audioWorklet.addModule("/pcm-worklet.js");
          const source = this.audioContext.createMediaStreamSource(this.micStream);
          const worklet = new AudioWorkletNode(this.audioContext, "pcm16-worklet");
          const silentOutput = this.audioContext.createGain();
          silentOutput.gain.value = 0;
          worklet.port.onmessage = (event) => {
            if (this.streamingMic && this.socket?.readyState === WebSocket.OPEN) this.socket.send(event.data);
          };
          source.connect(worklet).connect(silentOutput).connect(this.audioContext.destination);
          this.audioNodes = [source, worklet, silentOutput];
          this.setStatus("microphone", `ready (${Math.round(this.audioContext.sampleRate)} Hz context)`);
          if (activeMode === "open-mic" || activeMode === "hybrid") {
            await this.audioContext.resume();
            this.streamingMic = true;
            this.setStatus("turn", `${activeMode}: streaming continuously`);
          } else {
            this.setStatus("turn", "gated: hold to talk");
          }
        } catch (error) {
          this.operator.setReady(false);
          this.fail(`Microphone setup failed: ${error instanceof Error ? error.message : String(error)}`);
        }
      }
      async startGatedTurn() {
        if (!this.operator) return;
        if (!this.audioContext) {
          this.fail("Microphone is not ready; cannot start a gated turn.");
          return;
        }
        await this.audioContext.resume().catch((error) => {
          this.fail(`Could not resume microphone capture: ${error instanceof Error ? error.message : String(error)}`);
        });
        this.send({ t: "start-turn" });
        this.streamingMic = true;
        this.operator.setHoldActive(true);
        this.setStatus("turn", "recording gated turn");
      }
      endGatedTurn() {
        if (!this.streamingMic) return;
        this.stopMicStreaming();
        this.operator?.setHoldActive(false);
        this.send({ t: "end-turn" });
        this.setStatus("turn", "gated turn sent");
      }
      stopMicStreaming() {
        this.streamingMic = false;
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
        if (this.operator) this.operator.setStatus(name, value);
        else if (name === "connection" || name === "webrtc" || name === "avatar") this.view.setStatus(`${name}: ${value}`);
      }
      fail(message) {
        if (this.operator) this.operator.setError(message);
        else this.view.setError(message);
      }
      dispose() {
        if (this.pingId) window.clearInterval(this.pingId);
        this.stopMicStreaming();
        this.micStream?.getTracks().forEach((track) => track.stop());
        for (const node of this.audioNodes) node.disconnect();
        void this.audioContext?.close();
        this.pc?.close();
        this.socket?.close();
      }
    };
    function boot() {
      const viewName = new URLSearchParams(location.search).get("view") ?? "operator";
      const root = document.getElementById("app");
      if (!root) throw new Error("Missing #app root element.");
      const view = viewName === "display" ? renderDisplayView(root) : renderOperatorView(root);
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
