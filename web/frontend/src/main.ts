import { renderDisplayView, renderOperatorView, type DisplayView, type OperatorView, type ReadyConfig } from "./views";

type IceServerFrame = { urls: string[]; username?: string; credential?: string };
type ReadyFrame = { t: "ready"; config: ReadyConfig; iceServers: IceServerFrame[] };
type AvatarAnswerFrame = { t: "avatar-answer"; sdp: string };
type TranscriptFrame = { t: "user-transcript" | "agent-transcript"; text: string; final: boolean };
type ErrorFrame = { t: "error"; message: string };
type ServerFrame =
  | ReadyFrame
  | AvatarAnswerFrame
  | TranscriptFrame
  | ErrorFrame
  | { t: "speech-started" | "speech-stopped" | "avatar-speaking" | "avatar-idle" | "response-done" };

type ControlFrame =
  | { t: "avatar-offer"; sdp: string }
  | { t: "start-turn" }
  | { t: "end-turn" }
  | { t: "barge-in" }
  | { t: "say"; text: string }
  | { t: "ping" };

const wsUrl = `${location.protocol === "https:" ? "wss" : "ws"}://${location.host}/ws/session`;

function isOperatorView(view: OperatorView | DisplayView): view is OperatorView {
  return "holdButton" in view;
}

function parseServerFrame(data: string): ServerFrame {
  const frame = JSON.parse(data) as Partial<ServerFrame>;
  if (typeof frame.t !== "string") throw new Error("server frame missing t");
  return frame as ServerFrame;
}

function waitForIceGatheringComplete(pc: RTCPeerConnection): Promise<void> {
  if (pc.iceGatheringState === "complete") return Promise.resolve();
  return new Promise((resolve) => {
    const timeout = window.setTimeout(done, 2_500);
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

class ThinVoiceLiveClient {
  private readonly view: OperatorView | DisplayView;
  private readonly operator: OperatorView | undefined;
  private socket: WebSocket | undefined;
  private pc: RTCPeerConnection | undefined;
  private audioContext: AudioContext | undefined;
  private audioNodes: AudioNode[] = [];
  private micStream: MediaStream | undefined;
  private streamingMic = false;
  private readyConfig: ReadyConfig | undefined;
  private pingId = 0;

  constructor(view: OperatorView | DisplayView) {
    this.view = view;
    this.operator = isOperatorView(view) ? view : undefined;
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
    this.pingId = window.setInterval(() => this.send({ t: "ping" }), 25_000);
  }

  private async onMessage(event: MessageEvent) {
    if (typeof event.data !== "string") return;
    let frame: ServerFrame;
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

  private async onReady(frame: ReadyFrame) {
    this.readyConfig = frame.config;
    if (this.operator) {
      this.operator.clearError();
      this.operator.setConfig(frame.config);
      this.operator.setReady(true);
      this.wireOperatorControls(frame.config);
    } else {
      (this.view as DisplayView).setStatus(`Ready: ${frame.config.agentName}`);
    }
    this.setStatus("connection", "ready");

    await this.negotiateAvatar(frame.iceServers);
    if (this.operator) await this.prepareMicrophone(frame.config.activeMode);
  }

  private wireOperatorControls(config: ReadyConfig) {
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

  private async negotiateAvatar(iceServers: IceServerFrame[]) {
    this.setStatus("webrtc", "creating peer connection");
    try {
      this.pc = new RTCPeerConnection({
        iceServers: iceServers.map((server) => ({
          urls: server.urls,
          username: server.username,
          credential: server.credential,
        })),
      });
      this.pc.addTransceiver("video", { direction: "recvonly" });
      this.pc.addTransceiver("audio", { direction: "recvonly" });
      this.pc.ontrack = (event) => {
        const [stream] = event.streams;
        if (!stream) return;
        this.view.avatar.srcObject = stream;
        this.view.avatar.play().catch((error: unknown) => {
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

  private async onAvatarAnswer(sdp: string) {
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

  private async prepareMicrophone(activeMode: string) {
    if (!this.operator) return;
    try {
      this.setStatus("microphone", "requesting permission");
      this.micStream = await navigator.mediaDevices.getUserMedia({
        audio: { channelCount: 1, echoCancellation: true, noiseSuppression: true },
      });
      this.audioContext = new AudioContext({ sampleRate: 24_000 });
      await this.audioContext.audioWorklet.addModule("/pcm-worklet.js");

      const source = this.audioContext.createMediaStreamSource(this.micStream);
      const worklet = new AudioWorkletNode(this.audioContext, "pcm16-worklet");
      const silentOutput = this.audioContext.createGain();
      silentOutput.gain.value = 0;
      worklet.port.onmessage = (event: MessageEvent<ArrayBuffer>) => {
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

  private async startGatedTurn() {
    if (!this.operator) return;
    if (!this.audioContext) {
      this.fail("Microphone is not ready; cannot start a gated turn.");
      return;
    }
    await this.audioContext.resume().catch((error: unknown) => {
      this.fail(`Could not resume microphone capture: ${error instanceof Error ? error.message : String(error)}`);
    });
    this.send({ t: "start-turn" });
    this.streamingMic = true;
    this.operator.setHoldActive(true);
    this.setStatus("turn", "recording gated turn");
  }

  private endGatedTurn() {
    if (!this.streamingMic) return;
    this.stopMicStreaming();
    this.operator?.setHoldActive(false);
    this.send({ t: "end-turn" });
    this.setStatus("turn", "gated turn sent");
  }

  private stopMicStreaming() {
    this.streamingMic = false;
  }

  private sendSay(text: string) {
    if (text.trim().length === 0) return;
    this.send({ t: "say", text });
    this.setStatus("turn", "say sent");
  }

  private send(frame: ControlFrame) {
    if (this.socket?.readyState === WebSocket.OPEN) this.socket.send(JSON.stringify(frame));
  }

  private setStatus(name: "connection" | "speech" | "avatar" | "turn" | "webrtc" | "microphone", value: string) {
    if (this.operator) this.operator.setStatus(name, value);
    else if (name === "connection" || name === "webrtc" || name === "avatar") (this.view as DisplayView).setStatus(`${name}: ${value}`);
  }

  private fail(message: string) {
    if (this.operator) this.operator.setError(message);
    else (this.view as DisplayView).setError(message);
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
}

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
