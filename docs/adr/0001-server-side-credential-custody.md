# 0001 — The browser never holds an Azure credential

**Status:** Accepted

## Context

The browser needs a live Voice Live session. The simplest implementation mints a token in the browser and connects directly to Azure.

## Decision

The server holds all Azure credentials. It acquires a token via `DefaultAzureCredential` — a managed identity in Azure, developer credentials locally — opens the upstream Voice Live session itself, and relays control frames and audio uplink (browser → Azure) over `/ws/session`. The server never sends audio to the browser; avatar audio and video reach the browser exclusively over WebRTC. No token, key or connection string is ever sent to the browser.

## Alternatives rejected

- **Browser-minted ephemeral tokens.** Still puts an Azure-scoped credential in a context the operator's browser extensions, the venue network and anyone with the laptop can reach. The blast radius of a leak is the Foundry resource, not this app.
- **API keys in config.** Same exposure, without expiry.

## Consequences

- The Foundry resource is never directly reachable by a client. Compromising the browser yields an app session, not Azure access.
- The server is on the audio path for the uplink, so it must be sized for concurrent audio relay.
- Local development needs a signed-in developer identity with the right roles — the most common first-run failure. See [`../runbook.md`](../runbook.md) §4.
