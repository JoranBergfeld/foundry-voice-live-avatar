export type ReadyConfig = {
  activeMode: string;
  agentName: string;
  safeQuestions: string[];
  avatarCharacter?: string;
  avatarStyle?: string;
};

export type StatusName = "connection" | "speech" | "avatar" | "turn" | "webrtc" | "microphone";

export type OperatorView = {
  root: HTMLElement;
  avatar: HTMLVideoElement;
  holdButton: HTMLButtonElement;
  stopButton: HTMLButtonElement;
  repeatButton: HTMLButtonElement;
  safeQuestionButtons: HTMLButtonElement[];
  setConfig(config: ReadyConfig): void;
  setStatus(name: StatusName, value: string): void;
  setError(message: string): void;
  clearError(): void;
  setReady(ready: boolean): void;
  setHoldActive(active: boolean): void;
  addTranscript(role: "user" | "agent", text: string, final: boolean): void;
};

export type DisplayView = {
  root: HTMLElement;
  avatar: HTMLVideoElement;
  setStatus(message: string): void;
  setError(message: string): void;
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

export function renderOperatorView(root: HTMLElement): OperatorView {
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

  const safeQuestionButtons: HTMLButtonElement[] = [];
  const liveText: Record<"user" | "agent", string> = { user: "", agent: "" };

  shell.append(heading, error, avatarPanel, configPanel, statusPanel, controls, transcriptPanel);
  root.append(shell);

  function addTranscript(role: "user" | "agent", text: string, final: boolean) {
    const existing = transcriptList.querySelector<HTMLElement>(`.transcript-line.live.${role}`);
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
    addTranscript,
  };
}

export function renderDisplayView(root: HTMLElement): DisplayView {
  document.body.classList.add("display-view");
  root.replaceChildren();

  const video = document.createElement("video");
  video.id = "avatar";
  video.autoplay = true;
  video.playsInline = true;

  const overlay = document.createElement("div");
  overlay.className = "display-status";
  overlay.textContent = "Connecting to avatar session…";

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
    },
  };
}
