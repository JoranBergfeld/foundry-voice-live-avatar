# 0002 — Avatar media bypasses the server

**Status:** Accepted

## Context

The avatar produces a video and audio stream. Relaying it through the app server, as the control plane is relayed, would be architecturally uniform.

## Decision

Avatar media uses WebRTC **directly between the browser and Azure**. The server relays only the SDP offer/answer and ICE configuration; once negotiated, media never touches the app. Frame payload shapes are documented in [`../wire-protocol.md`](../wire-protocol.md).

## Alternatives rejected

- **Server-relayed media.** Adds a hop of latency to a live stage performance and makes the B1 App Service instance a video relay, which it cannot do at acceptable quality.

## Consequences

- Lowest achievable latency, and video quality is independent of app instance size — important, because the whole point is a believable on-stage presence.
- **The venue's network must reach Azure directly over WebRTC.** Restrictive venue firewalls break the avatar while leaving the control plane working, which presents as a working session with no avatar video or audio. Test from the actual stage position on the actual network.
- The server cannot observe, record or moderate avatar output. What Azure renders is what the audience sees.
- `avatar-error` is a **media-plane failure**, not a session failure. `handleAvatarError` closes the `RTCPeerConnection`; both avatar video **and audio** are lost because both `recvonly` transceivers ride that single peer connection. The WebSocket, concurrency slot, microphone capture and transcripts survive, but the room receives no avatar output. The failure mode is a working session with no avatar video or audio — the operator must invoke a fallback plan. A control-plane `error` ends the session entirely.
