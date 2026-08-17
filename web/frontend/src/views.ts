export type ReadyConfig = {
  mode?: string;
  activeMode: string;
  agentName: string;
  safeQuestions: string[];
  avatarCharacter?: string;
  avatarStyle?: string;
};

export type StatusName = "connection" | "speech" | "avatar" | "turn" | "webrtc" | "microphone";

export type InteractiveView = {
  root: HTMLElement;
  avatar: HTMLVideoElement;
  holdButton: HTMLButtonElement;
  setConfig(config: ReadyConfig): void;
  setStatus(name: StatusName, value: string): void;
  setError(message: string): void;
  clearError(): void;
  setReady(ready: boolean): void;
  setReconnectHandler(handler: () => void): void;
  setDisconnected(disconnected: boolean): void;
  setHoldActive(active: boolean): void;
  addTranscript(role: "user" | "agent", text: string, final: boolean): void;
  noteNonFatal(message: string): void;
  supportsMuteToggle?: boolean;
  setMuted?(muted: boolean): void;
  stopButton?: HTMLButtonElement;
  repeatButton?: HTMLButtonElement;
  safeQuestionButtons?: HTMLButtonElement[];
  noteTool?(text: string): void;
};

export type SubtitleView = {
  setAgentSubtitle(text: string, final: boolean): void;
  noteAgentSpeaking(): void;
  noteAgentIdle(): void;
  completeAgentSubtitle(): void;
  clearAgentSubtitle(): void;
};

export type OperatorView = InteractiveView & {
  stopButton: HTMLButtonElement;
  repeatButton: HTMLButtonElement;
  safeQuestionButtons: HTMLButtonElement[];
  noteTool(text: string): void;
};

export type LandingView = InteractiveView & SubtitleView & {
  supportsMuteToggle: true;
  setMuted(muted: boolean): void;
};

export type DisplayView = SubtitleView & {
  root: HTMLElement;
  avatar: HTMLVideoElement;
  setStatus(message: string): void;
  setError(message: string): void;
  clearError(): void;
  setReconnectHandler(handler: () => void): void;
  setDisconnected(disconnected: boolean): void;
};

function button(label: string): HTMLButtonElement {
  const element = document.createElement("button");
  element.type = "button";
  element.textContent = label;
  element.disabled = true;
  return element;
}

function statusLine(label: string): HTMLParagraphElement {
  const line = document.createElement("p");
  line.className = "status-line";
  line.dataset.label = label;
  line.textContent = `${label}: pending`;
  return line;
}

function setText(element: HTMLElement, value: string) {
  element.textContent = value;
}

// Subtitles are revealed word-by-word, paced to a speech-rate estimate, instead of dumping
// the whole transcript the instant it streams in (the spoken-audio transcript arrives far
// faster than the avatar voices it). Design notes learned from tuning against the live avatar:
//   - The reveal STARTS the moment the avatar begins speaking, showing the first word
//     immediately so the caption is not a beat behind the voice.
//   - We do NOT fade on `avatar-idle`: that event tracks the avatar's idle animation and
//     fires during natural pauses, so it is an unreliable "done" signal and caused the caption
//     to vanish mid-sentence. We also do not fade on `response-done` alone, since the model
//     finishes generating text seconds before the avatar finishes voicing it.
//   - Instead the caption fades only once the FULL line is on screen AND the response is
//     complete, after a generous hold. Because the reveal is paced to roughly match speech,
//     full-reveal lands near the end of the audio; the hold then covers any remaining tail.
// Pacing is adaptive to word length so longer words linger, roughly tracking ~180 wpm speech.
// These are deliberately a touch faster than average speech so the reveal keeps up with (and
// finishes alongside) the voice rather than trailing behind it.
const REVEAL_BASE_MS = 80; // fixed per-word cost
const REVEAL_PER_CHAR_MS = 38; // added per character so longer words take longer
const REVEAL_MIN_MS = 150;
const REVEAL_MAX_MS = 460;
const HOLD_MS = 2600; // how long the finished line lingers after the full text is shown
const FADE_MS = 300;

function nextWordBoundary(text: string, from: number): number {
  let index = from;
  while (index < text.length && text[index] === " ") index += 1;
  while (index < text.length && text[index] !== " ") index += 1;
  return index;
}

function revealDelayForWord(wordLength: number): number {
  return Math.min(REVEAL_MAX_MS, Math.max(REVEAL_MIN_MS, REVEAL_BASE_MS + REVEAL_PER_CHAR_MS * wordLength));
}

function createSubtitleController(toggle: HTMLButtonElement, overlay: HTMLElement): SubtitleView {
  let enabled = false;
  let fullText = ""; // authoritative transcript accumulated for the current turn
  let revealed = 0; // characters of fullText currently shown
  let started = false; // avatar has begun voicing this turn: the reveal may run
  let complete = false; // response-done received: no more text is coming this turn
  let awaitingNewTurn = false; // caption is holding/fading; next activity starts a new turn
  let revealTimer: ReturnType<typeof setTimeout> | undefined;
  let holdTimer: ReturnType<typeof setTimeout> | undefined;
  let fadeClearTimer: ReturnType<typeof setTimeout> | undefined;

  const cancelRevealTimer = () => {
    if (revealTimer !== undefined) clearTimeout(revealTimer);
    revealTimer = undefined;
  };

  const cancelFadeTimers = () => {
    if (holdTimer !== undefined) clearTimeout(holdTimer);
    if (fadeClearTimer !== undefined) clearTimeout(fadeClearTimer);
    holdTimer = undefined;
    fadeClearTimer = undefined;
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
    if (holdTimer !== undefined || fadeClearTimer !== undefined) return;
    awaitingNewTurn = true;
    holdTimer = setTimeout(() => {
      holdTimer = undefined;
      overlay.classList.add("fading");
      fadeClearTimer = setTimeout(() => {
        fadeClearTimer = undefined;
        clearPresentation();
      }, FADE_MS);
    }, HOLD_MS);
  };

  const showRevealed = () => {
    overlay.textContent = fullText.slice(0, revealed);
    overlay.classList.remove("fading");
    overlay.hidden = false;
  };

  // Advance the caption one word and re-arm the timer. Once the whole line is shown and the
  // response is complete, begin the hold + fade; otherwise wait for more text.
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
      revealTimer = undefined;
      revealed = boundary;
      showRevealed();
      pump();
    }, revealDelayForWord(wordLength));
  };

  // A previous caption is holding/fading and fresh activity arrived: this is a new turn.
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
      // Show the first word immediately so the caption tracks the voice from the first beat.
      if (revealTimer === undefined && revealed < fullText.length) {
        revealed = nextWordBoundary(fullText, revealed);
        showRevealed();
      }
      pump();
    },
    noteAgentIdle() {
      // Deliberately not used to fade: avatar-idle tracks the idle animation and fires during
      // pauses, so it is an unreliable "the avatar has finished" signal. Fading is driven by
      // full-reveal + response-done + hold instead.
    },
    completeAgentSubtitle() {
      complete = true;
      if (!enabled || (overlay.hidden && fullText.length === 0)) {
        resetState();
        return;
      }
      // Ensure the tail can still reveal even if no further speaking event arrives.
      started = true;
      pump();
    },
    clearAgentSubtitle() {
      cancelRevealTimer();
      cancelFadeTimers();
      clearPresentation();
    },
  };
}

function createTranscriptAppender(list: HTMLElement) {
  const liveText: Record<"user" | "agent", string> = { user: "", agent: "" };
  return function addTranscript(role: "user" | "agent", text: string, final: boolean) {
    const existing = list.querySelector<HTMLElement>(`.transcript-line.live.${role}`);
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

export function renderOperatorView(root: HTMLElement): OperatorView {
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

  const statuses = new Map<StatusName, HTMLParagraphElement>();
  const statusPanel = document.createElement("section");
  statusPanel.className = "status-panel";
  for (const name of ["connection", "webrtc", "microphone", "turn", "speech", "avatar"] as StatusName[]) {
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

  const safeQuestionButtons: HTMLButtonElement[] = [];

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
      const stamp = new Date().toLocaleTimeString();
      line.textContent = `${stamp} — ${text}`;
      toolsList.append(line);
      while (toolsList.childElementCount > 8) toolsList.firstElementChild?.remove();
      line.scrollIntoView({ block: "nearest" });
    },
    noteNonFatal(message) {
      nonFatal.hidden = false;
      nonFatal.textContent = message;
    },
  };
}

export function renderLandingView(root: HTMLElement): LandingView {
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
  gear.textContent = "⚙ Config";
  gear.setAttribute("aria-label", "Config");
  gear.title = "Config";

  const holdButton = document.createElement("button");
  holdButton.type = "button";
  holdButton.className = "landing-talk";
  holdButton.textContent = "🎤 Hold to talk";
  holdButton.disabled = true;
  holdButton.hidden = true;

  const transcriptToggle = document.createElement("button");
  transcriptToggle.type = "button";
  transcriptToggle.className = "landing-action landing-transcript-toggle";
  transcriptToggle.textContent = "💬 Transcript";
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
  panelClose.textContent = "×";
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
      // The landing screen is intentionally minimal; config drives only the talk control.
    },
    setStatus(name, value) {
      // The pill is only a transient connection indicator. Routine per-turn
      // avatar states (speaking/idle) are conveyed by the avatar itself and
      // must not resurface the pill after the connection is established.
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
      holdButton.textContent = active ? "🎤 Release to end turn" : "🎤 Hold to talk";
    },
    setMuted(muted) {
      holdButton.classList.toggle("muted", muted);
      holdButton.textContent = muted ? "🔇 Muted — tap to unmute" : "🎤 Listening — tap to mute";
    },
    addTranscript,
    noteNonFatal(message) {
      notice.hidden = false;
      notice.textContent = message;
    },
  };
}

export function renderDisplayView(root: HTMLElement): DisplayView {
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
  message.textContent = "Connecting to avatar session…";

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
    },
  };
}
