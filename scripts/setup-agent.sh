#!/usr/bin/env bash
set -euo pipefail

# ---------------------------------------------------------------------------
# setup-agent.sh — postprovision hook
#
# Detects existing Azure AI Foundry Voice Live agents and instructs the
# operator how to enable agent mode.  The app runs in MODEL mode (gpt-realtime)
# out-of-box; agent mode is a documented opt-in that reuses a pre-existing,
# portal-created Voice Live agent.
#
# This script NEVER creates or modifies agents.  It only issues a GET request.
# Always exits 0 so that `continueOnError: true` is never triggered by a crash.
# ---------------------------------------------------------------------------

echo ""
echo "=== Foundry Voice Live — agent detection ==="
echo ""

# ---------- 1. Resolve project endpoint ------------------------------------
if [ -n "${AZURE_AI_PROJECT_ENDPOINT:-}" ]; then
  PROJECT_ENDPOINT="${AZURE_AI_PROJECT_ENDPOINT}"
elif [ -n "${AZURE_AI_SERVICES_NAME:-}" ] && [ -n "${AZURE_AI_PROJECT_NAME:-}" ]; then
  PROJECT_ENDPOINT="https://${AZURE_AI_SERVICES_NAME}.services.ai.azure.com/api/projects/${AZURE_AI_PROJECT_NAME}"
else
  echo "Skipping agent detection: project env vars not set."
  echo "The app runs in MODEL mode (gpt-realtime) — no agent required."
  echo ""
  exit 0
fi

echo "Project endpoint: ${PROJECT_ENDPOINT}"
echo ""

# ---------- 2. Require jq --------------------------------------------------
if ! command -v jq &>/dev/null; then
  echo "Note: 'jq' is not installed — skipping agent detection."
  echo "The app runs in MODEL mode (gpt-realtime) — no agent required."
  echo "Install jq and re-run 'azd provision' to get agent-detection output."
  echo ""
  exit 0
fi

# ---------- 3. List agents (GET only — never POST/PUT/PATCH/DELETE) --------
echo "Listing Voice Live agents at ${PROJECT_ENDPOINT}/agents?api-version=v1 ..."
AGENTS_JSON="$(az rest \
  --method GET \
  --resource https://ai.azure.com \
  --url "${PROJECT_ENDPOINT}/agents?api-version=v1" \
  -o json 2>/dev/null || echo '{}')"

# ---------- 4. Extract Voice Live–enabled agent names ----------------------
VL_AGENTS="$(printf '%s' "${AGENTS_JSON}" | \
  jq -r '.data[]? | select(.versions.latest.metadata["microsoft.voice-live.enabled"] == "true") | .name' \
  2>/dev/null || true)"

# ---------- 5. Report -------------------------------------------------------
if [ -n "${VL_AGENTS}" ]; then
  echo "✅ Found Voice Live–enabled agent(s):"
  while IFS= read -r agent_name; do
    echo "   • ${agent_name}"
  done <<< "${VL_AGENTS}"

  FIRST_AGENT="$(printf '%s' "${VL_AGENTS}" | head -n1)"
  PROJECT_NAME="${AZURE_AI_PROJECT_NAME:-proj-default}"

  echo ""
  echo "────────────────────────────────────────────────────────────────"
  echo " How to switch the app to AGENT mode:"
  echo "────────────────────────────────────────────────────────────────"
  echo ""
  echo " 1. Edit config/agent.json and set:"
  echo "      \"agentName\": \"${FIRST_AGENT}\","
  echo "      \"agentProjectName\": \"${PROJECT_NAME}\""
  echo ""
  echo " 2. Run:  azd env set VOICELIVE_MODE agent"
  echo ""
  echo " 3. Run:  azd up"
  echo "    (or:  azd provision && azd deploy)"
  echo ""
  echo "────────────────────────────────────────────────────────────────"
  echo " The app currently runs in MODEL mode (gpt-realtime) until you"
  echo " complete the steps above."
  echo "────────────────────────────────────────────────────────────────"
else
  echo "ℹ️  No Voice Live–enabled agents found in this project."
  echo ""
  echo "The app is running in MODEL mode (gpt-realtime) out-of-box —"
  echo "no agent is required for basic operation."
  echo ""
  echo "────────────────────────────────────────────────────────────────"
  echo " To use AGENT mode, first create a Voice Live agent:"
  echo "────────────────────────────────────────────────────────────────"
  echo ""
  echo " 1. Open the Azure AI Foundry portal: https://ai.azure.com"
  echo "    Navigate to your project and create a Voice Live agent."
  echo ""
  echo " 2. Edit config/agent.json and set:"
  echo "      \"agentName\": \"<your-agent-name>\","
  echo "      \"agentProjectName\": \"${AZURE_AI_PROJECT_NAME:-proj-default}\""
  echo ""
  echo " 3. Run:  azd env set VOICELIVE_MODE agent"
  echo ""
  echo " 4. Run:  azd up"
  echo "    (or:  azd provision && azd deploy)"
  echo ""
  echo "────────────────────────────────────────────────────────────────"
fi

echo ""
exit 0
