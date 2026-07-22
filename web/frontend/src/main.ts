import { renderOperatorView, renderDisplayView } from "./views";

type ClientConfig = { region: string; model: string; activeMode: string; agentName: string; safeQuestions: string[] };

async function boot() {
  const view = new URLSearchParams(location.search).get("view") ?? "operator";
  const root = document.getElementById("app")!;
  const cfg = (await (await fetch("/api/config")).json()) as ClientConfig;
  if (view === "display") renderDisplayView(root);
  else renderOperatorView(root, cfg);
  // Phase 7: import "@azure/ai-voicelive", fetch /api/token, open agent-mode session, negotiate avatar WebRTC.
}
boot().catch((e) => { document.body.innerHTML = `<pre style="color:red">Startup failed: ${e}</pre>`; });
