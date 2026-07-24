# ---------------------------------------------------------------------------
# setup-agent.ps1 — postprovision hook
#
# Detects existing Azure AI Foundry Voice Live agents and instructs the
# operator how to enable agent mode.  The app runs in MODEL mode (gpt-realtime)
# out-of-box; agent mode is a documented opt-in that reuses a pre-existing,
# portal-created Voice Live agent.
#
# This script NEVER creates or modifies agents.  It only issues a GET request.
# Always exits 0 so that `continueOnError: true` is never triggered by a crash.
# ---------------------------------------------------------------------------

$ErrorActionPreference = 'Stop'

Write-Host ""
Write-Host "=== Foundry Voice Live — agent detection ==="
Write-Host ""

# ---------- 1. Resolve project endpoint ------------------------------------
$projectEndpoint = $null

if ($env:AZURE_AI_PROJECT_ENDPOINT) {
    $projectEndpoint = $env:AZURE_AI_PROJECT_ENDPOINT
} elseif ($env:AZURE_AI_SERVICES_NAME -and $env:AZURE_AI_PROJECT_NAME) {
    $projectEndpoint = "https://$($env:AZURE_AI_SERVICES_NAME).services.ai.azure.com/api/projects/$($env:AZURE_AI_PROJECT_NAME)"
} else {
    Write-Host "Skipping agent detection: project env vars not set."
    Write-Host "The app runs in MODEL mode (gpt-realtime) — no agent required."
    Write-Host ""
    exit 0
}

Write-Host "Project endpoint: $projectEndpoint"
Write-Host ""

# ---------- 2. List agents (GET only — never POST/PUT/PATCH/DELETE) --------
Write-Host "Listing Voice Live agents at ${projectEndpoint}/agents?api-version=v1 ..."

$agentsData = $null
try {
    $agentsJson = az rest `
        --method GET `
        --resource https://ai.azure.com `
        --url "${projectEndpoint}/agents?api-version=v1" `
        -o json 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "az rest returned exit code $LASTEXITCODE"
    }
    $agentsData = $agentsJson | ConvertFrom-Json
} catch {
    Write-Host "Note: Could not retrieve agents list: $_"
    Write-Host "The app runs in MODEL mode (gpt-realtime) — no agent required."
    Write-Host ""
    exit 0
}

# ---------- 3. Extract Voice Live–enabled agent names ----------------------
$vlAgents = @()
try {
    if ($agentsData -and $agentsData.data) {
        foreach ($agent in $agentsData.data) {
            $metadata = $agent.versions.latest.metadata
            if ($metadata -and $metadata.'microsoft.voice-live.enabled' -eq 'true') {
                $vlAgents += $agent.name
            }
        }
    }
} catch {
    Write-Host "Note: Could not parse agents response: $_"
    Write-Host "The app runs in MODEL mode (gpt-realtime) — no agent required."
    Write-Host ""
    exit 0
}

$projectName = if ($env:AZURE_AI_PROJECT_NAME) { $env:AZURE_AI_PROJECT_NAME } else { 'proj-default' }

# ---------- 4. Report -------------------------------------------------------
if ($vlAgents.Count -gt 0) {
    Write-Host "✅ Found Voice Live–enabled agent(s):"
    foreach ($agentName in $vlAgents) {
        Write-Host "   • $agentName"
    }

    $firstAgent = $vlAgents[0]

    Write-Host ""
    Write-Host "────────────────────────────────────────────────────────────────"
    Write-Host " How to switch the app to AGENT mode:"
    Write-Host "────────────────────────────────────────────────────────────────"
    Write-Host ""
    Write-Host " 1. Edit config/agent.json and set:"
    Write-Host "      ""agentName"": ""$firstAgent"","
    Write-Host "      ""agentProjectName"": ""$projectName"""
    Write-Host ""
    Write-Host " 2. Run:  azd env set VOICELIVE_MODE agent"
    Write-Host ""
    Write-Host " 3. Run:  azd up"
    Write-Host "    (or:  azd provision && azd deploy)"
    Write-Host ""
    Write-Host "────────────────────────────────────────────────────────────────"
    Write-Host " The app currently runs in MODEL mode (gpt-realtime) until you"
    Write-Host " complete the steps above."
    Write-Host "────────────────────────────────────────────────────────────────"
} else {
    Write-Host "ℹ️  No Voice Live–enabled agents found in this project."
    Write-Host ""
    Write-Host "The app is running in MODEL mode (gpt-realtime) out-of-box —"
    Write-Host "no agent is required for basic operation."
    Write-Host ""
    Write-Host "────────────────────────────────────────────────────────────────"
    Write-Host " To use AGENT mode, first create a Voice Live agent:"
    Write-Host "────────────────────────────────────────────────────────────────"
    Write-Host ""
    Write-Host " 1. Open the Azure AI Foundry portal: https://ai.azure.com"
    Write-Host "    Navigate to your project and create a Voice Live agent."
    Write-Host ""
    Write-Host " 2. Edit config/agent.json and set:"
    Write-Host "      ""agentName"": ""<your-agent-name>"","
    Write-Host "      ""agentProjectName"": ""$projectName"""
    Write-Host ""
    Write-Host " 3. Run:  azd env set VOICELIVE_MODE agent"
    Write-Host ""
    Write-Host " 4. Run:  azd up"
    Write-Host "    (or:  azd provision && azd deploy)"
    Write-Host ""
    Write-Host "────────────────────────────────────────────────────────────────"
}

Write-Host ""
exit 0
