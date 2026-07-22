# Config schema

The `/config` directory contains four JSON files shared by the CLI and web apps. All values below mirror the default config files.

## `session.json`

| Field | Type | Required | Allowed values / default | Description |
| --- | --- | --- | --- | --- |
| `endpoint` | string | Required | Default: `wss://REPLACE-ME.services.ai.azure.com` | Voice Live websocket endpoint. |
| `region` | string | Required | Default: `swedencentral` | Azure region for the Voice Live resource. |
| `apiVersion` | string | Required | Default: `2026-04-10` | Voice Live API version. |
| `model` | string | Required | Default: `gpt-realtime` | Realtime model deployment name. |
| `voice` | object | Required | Contains `type`, `name` | Voice selection. |
| `voice.type` | string | Required | `azure-realtime-native`, `azure-standard`, `azure-custom`, `openai`; default: `azure-realtime-native` | Voice provider/type. |
| `voice.name` | string | Required | Default: `andrew` | Voice name. |
| `inputAudioSamplingRate` | number | Required | Default: `24000` | Input audio sampling rate in hertz. |
| `inputAudioNoiseReduction` | object | Required | Contains `type` | Input audio noise reduction settings. |
| `inputAudioNoiseReduction.type` | string | Required | Default: `azure_deep_noise_suppression` | Noise reduction mode. |
| `inputAudioEchoCancellation` | object | Required | Contains `type` | Input audio echo cancellation settings. |
| `inputAudioEchoCancellation.type` | string | Required | Default: `server_echo_cancellation` | Echo cancellation mode. |
| `inputAudioTranscription` | object | Required | Contains `model`, `language` | Input audio transcription settings. |
| `inputAudioTranscription.model` | string | Required | Default: `azure-speech` | Transcription model. |
| `inputAudioTranscription.language` | string | Required | Default: `en` | Transcription language. |

## `turntaking.json`

| Field | Type | Required | Allowed values / default | Description |
| --- | --- | --- | --- | --- |
| `activeMode` | string | Required | `open-mic`, `gated`, `hybrid`; default: `gated` | The active turn-taking mode. |
| `modes` | object | Required | Contains `open-mic`, `gated`, `hybrid` | Available turn-taking mode definitions. |
| `modes.open-mic` | object | Required | Contains `manualTurn`, `turnDetection` | Open microphone mode definition. |
| `modes.open-mic.manualTurn` | boolean | Required | Default: `false` | Whether turns are manually committed. |
| `modes.open-mic.turnDetection` | object | Required | Contains semantic VAD settings | Automatic turn detection settings for open mic. |
| `modes.open-mic.turnDetection.type` | string | Required | Default: `azure_semantic_vad` | VAD implementation. |
| `modes.open-mic.turnDetection.threshold` | number | Required | Default: `0.5` | VAD confidence threshold. |
| `modes.open-mic.turnDetection.prefixPaddingMs` | number | Required | Default: `420` | Audio padding before detected speech, in milliseconds. |
| `modes.open-mic.turnDetection.silenceDurationMs` | number | Required | Default: `500` | Silence duration before ending a turn, in milliseconds. |
| `modes.open-mic.turnDetection.interruptResponse` | boolean | Required | Default: `true` | Whether detected speech can interrupt the avatar response. |
| `modes.open-mic.turnDetection.endOfUtteranceDetection` | object | Required | Contains `model`, `thresholdLevel`, `timeoutMs` | Semantic end-of-utterance settings. |
| `modes.open-mic.turnDetection.endOfUtteranceDetection.model` | string | Required | Default: `semantic_detection_v1` | End-of-utterance model. |
| `modes.open-mic.turnDetection.endOfUtteranceDetection.thresholdLevel` | string | Required | Default: `medium` | End-of-utterance threshold level. |
| `modes.open-mic.turnDetection.endOfUtteranceDetection.timeoutMs` | number | Required | Default: `1000` | End-of-utterance timeout in milliseconds. |
| `modes.gated` | object | Required | Contains `manualTurn`, `interruptResponse` | Gated mode definition. |
| `modes.gated.manualTurn` | boolean | Required | Default: `true` | Whether turns are manually committed. |
| `modes.gated.interruptResponse` | boolean | Required | Default: `false` | Whether input interrupts the avatar response. |
| `modes.hybrid` | object | Required | Contains `manualTurn`, `gateGatesBargeIn`, `turnDetection` | Hybrid mode definition. |
| `modes.hybrid.manualTurn` | boolean | Required | Default: `false` | Whether turns are manually committed. |
| `modes.hybrid.gateGatesBargeIn` | boolean | Required | Default: `true` | Whether the gate controls barge-in behavior. |
| `modes.hybrid.turnDetection` | object | Required | Contains semantic VAD settings | Automatic turn detection settings for hybrid mode. |
| `modes.hybrid.turnDetection.type` | string | Required | Default: `azure_semantic_vad` | VAD implementation. |
| `modes.hybrid.turnDetection.threshold` | number | Required | Default: `0.5` | VAD confidence threshold. |
| `modes.hybrid.turnDetection.silenceDurationMs` | number | Required | Default: `500` | Silence duration before ending a turn, in milliseconds. |
| `modes.hybrid.turnDetection.interruptResponse` | boolean | Required | Default: `true` | Whether detected speech can interrupt the avatar response. |
| `modes.hybrid.turnDetection.endOfUtteranceDetection` | object | Required | Contains `model`, `thresholdLevel`, `timeoutMs` | Semantic end-of-utterance settings. |
| `modes.hybrid.turnDetection.endOfUtteranceDetection.model` | string | Required | Default: `semantic_detection_v1` | End-of-utterance model. |
| `modes.hybrid.turnDetection.endOfUtteranceDetection.thresholdLevel` | string | Required | Default: `medium` | End-of-utterance threshold level. |
| `modes.hybrid.turnDetection.endOfUtteranceDetection.timeoutMs` | number | Required | Default: `1000` | End-of-utterance timeout in milliseconds. |

## `agent.json`

| Field | Type | Required | Allowed values / default | Description |
| --- | --- | --- | --- | --- |
| `agentName` | string | Required | Default: `company-direction-avatar` | Foundry agent name. |
| `agentProjectName` | string | Required | Default: `proj-default` | Foundry agent project name (the short project name in the Foundry endpoint path, e.g. `proj-default`). |
| `agentVersion` | string or null | Optional | Default: `null` | Optional pinned agent version. |
| `conversationResumePolicy` | string | Required | `resume`, `fresh`; default: `resume` | Whether conversations resume or start fresh. |
| `groundingStrategy` | string | Required | `pack`, `rag`, `both`; default: `pack` | Grounding source strategy. |
| `safeQuestions` | string array | Required | Default: two configured fallback questions | Safe redirect questions the avatar can use. |
| `safeQuestions[]` | string | Required | Defaults: `Let's refocus - what is our single most important priority this year?`, `What does this direction mean for our customers?` | Individual safe redirect question. |

## `avatar.json`

| Field | Type | Required | Allowed values / default | Description |
| --- | --- | --- | --- | --- |
| `character` | string | Required | Default: `lisa` | Avatar character. |
| `style` | string | Required | Default: `casual-sitting` | Avatar style. |
| `customized` | boolean | Required | Default: `false` | Whether a customized avatar is used. |
| `video` | object | Required | Contains `resolution`, `bitrate`, `codec` | Video output settings. |
| `video.resolution` | object | Required | Contains `width`, `height` | Video resolution. |
| `video.resolution.width` | number | Required | Default: `1920` | Video width in pixels. |
| `video.resolution.height` | number | Required | Default: `1080` | Video height in pixels. |
| `video.bitrate` | number | Required | Default: `2000000` | Video bitrate in bits per second. |
| `video.codec` | string | Required | Default: `h264` | Video codec. |

## Validation rules

- All fields marked required above must be present and non-empty where applicable.
- `session.json.voice.type` must be one of `azure-realtime-native`, `azure-standard`, `azure-custom`, or `openai`.
- `turntaking.json.activeMode` must be one of `open-mic`, `gated`, or `hybrid`, and the matching entry must exist in `modes`.
- `agent.json.groundingStrategy` must be one of `pack`, `rag`, or `both`.
- `agent.json.conversationResumePolicy` must be one of `resume` or `fresh`.
- Unknown values for `voice.type`, `turntaking.activeMode`, `agent.groundingStrategy`, or `agent.conversationResumePolicy` fail fast at startup.
