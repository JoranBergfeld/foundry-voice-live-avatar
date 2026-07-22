export type ClientConfig = {
  region: string;
  model: string;
  activeMode: string;
  agentName: string;
  safeQuestions: string[];
};

function makeButton(label: string): HTMLButtonElement {
  const button = document.createElement("button");
  button.type = "button";
  button.textContent = label;
  button.disabled = true;
  return button;
}

export function renderOperatorView(root: HTMLElement, cfg: ClientConfig) {
  document.body.classList.remove("display-view");
  root.replaceChildren();

  const shell = document.createElement("main");
  shell.className = "operator-shell";

  const heading = document.createElement("h1");
  heading.textContent = "Voice Live Operator";

  const deviceLabel = document.createElement("label");
  deviceLabel.textContent = "Audio input device";
  const deviceSelect = document.createElement("select");
  deviceSelect.disabled = true;
  const placeholder = document.createElement("option");
  placeholder.textContent = "Default microphone (Phase 7 wiring)";
  deviceSelect.append(placeholder);
  deviceLabel.append(deviceSelect);

  const gateButton = makeButton("Hold to talk (gate)");

  const modeLabel = document.createElement("p");
  modeLabel.textContent = `Turn-taking: ${cfg.activeMode} · Agent: ${cfg.agentName}`;

  const panicControls = document.createElement("section");
  panicControls.className = "panic-controls";
  panicControls.append(makeButton("Stop speaking"), makeButton("Repeat last answer"));

  for (const question of cfg.safeQuestions) {
    panicControls.append(makeButton(question));
  }

  shell.append(heading, deviceLabel, gateButton, modeLabel, panicControls);
  root.append(shell);
}

export function renderDisplayView(root: HTMLElement) {
  document.body.classList.add("display-view");
  root.replaceChildren();

  const video = document.createElement("video");
  video.id = "avatar";
  video.autoplay = true;
  video.playsInline = true;
  root.append(video);
}
