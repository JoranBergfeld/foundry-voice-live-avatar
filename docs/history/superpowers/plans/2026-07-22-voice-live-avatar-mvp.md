# Voice Live Avatar — MVP Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Stand up the buildable, tested foundation of the two-app Voice Live Avatar system — shared `/config`, the CLI `validate` tool, the web token-broker backend, a frontend scaffold, and CI — all green on this Linux/WSL box, with live-Azure session wiring delimited as a post-MVP phase.

**Architecture:** Two independent .NET 10 solutions that share nothing but `/config` and this spec. The CLI (rehearsal harness) and web (show client) each own their own config models/validators (deliberate duplication, per spec §3.3). The MVP proves the config→Voice-Live-wire mapping and the web token-broker/config endpoints without requiring live Azure; the real `Azure.AI.VoiceLive` / `@azure/ai-voicelive` session wiring is Phase 7 (needs a provisioned Foundry resource + credentials).

**Tech Stack:** .NET 10 (C#), xUnit, ASP.NET Core minimal APIs, `Microsoft.AspNetCore.Mvc.Testing`, `Azure.Identity`, vanilla TypeScript + esbuild, `@azure/ai-voicelive` (Phase 7), GitHub Actions (validated with `actionlint`).

**Source of truth:** `docs/superpowers/specs/2026-07-22-voice-live-avatar-design.md`. Exact Voice Live wire field names come from that spec's §2 and Appendix A.

---

## MVP definition (what "done for lunch" means)

- `dotnet test` is green for `/cli` and `/web`.
- `cd cli/src/VoiceLive.Cli && dotnet run -- validate --config ../../../config` prints the resolved `session.update` payload and exits 0; exits non-zero with a clear file/field message on bad config.
- `cd web/src/VoiceLive.Web && dotnet run` serves `GET /api/config` (validated JSON) and `GET /api/health`; `GET /api/token` fails **clearly** (HTTP 502 + explicit message) when Azure credentials are absent — it never returns a fake token.
- `cd web/frontend && npm run build` produces `wwwroot/app.js`; opening the served page shows the operator + display view scaffold and fetches `/api/config`.
- CI workflow builds+tests both apps independently and passes `actionlint`.

Phases 1–6 are the MVP. **Phase 7 (live Azure) is explicitly out of the lunch window** and must not be faked.

---

## File structure

```
/README.md                         # orientation (Task 1)
/.gitignore                        # dotnet + node (Task 1)
/config/
  session.json  turntaking.json  agent.json  avatar.json           # Task 2
  grounding/company-direction.md                                    # Task 2
/docs/config-schema.md                                             # Task 2
/cli/
  VoiceLive.Cli.sln
  src/VoiceLive.Cli/            # console app (Tasks 3-7)
    Config/...  Session/...  Program.cs
  tests/VoiceLive.Cli.Tests/   # xUnit (Tasks 4-7)
/web/
  VoiceLive.Web.sln
  src/VoiceLive.Web/           # ASP.NET Core (Tasks 8-10)
    Config/...  Tokens/...  Program.cs  wwwroot/
  tests/VoiceLive.Web.Tests/   # xUnit + Mvc.Testing (Tasks 8-9)
  frontend/                    # vanilla TS + esbuild (Task 11)
/pipeline/ci.yml                                                   # Task 12
/tools/                        # sync-agent - Phase 7 (post-MVP)
```

**Duplication is intentional:** `/cli` and `/web` each define their own `SessionConfig`, `TurnTakingConfig`, `AgentConfig`, and validators. Do **not** create a shared project.

---

## Task 1: Repo scaffolding

**Files:**
- Create: `README.md`, `.gitignore`, `cli/VoiceLive.Cli.sln`, `web/VoiceLive.Web.sln`

- [ ] **Step 1: Create folders and .gitignore**

```bash
cd /home/jbergfeld/vcs/foundry-voice-live-avatar
mkdir -p cli/src cli/tests web/src web/tests web/frontend config/grounding pipeline tools
cat > .gitignore <<'EOF'
# .NET
bin/
obj/
*.user
# Node
node_modules/
web/src/VoiceLive.Web/wwwroot/app.js
web/src/VoiceLive.Web/wwwroot/app.js.map
# OS/editor
.DS_Store
.vs/
EOF
```

- [ ] **Step 2: Replace root README (orientation only, no code)**

```bash
cat > README.md <<'EOF'
# foundry-voice-live-avatar

A conversational avatar on Microsoft Foundry's Voice Live API, built as **two independent apps**:

- [`/cli`](./cli) - voice-only rehearsal harness (fast prompt/turn-taking/voice tuning). Windows for audio.
- [`/web`](./web) - the on-stage show client: token-broker backend + browser avatar via `@azure/ai-voicelive`.
- [`/config`](./config) - shared runtime configuration (no code). Both apps validate it at startup.
- [`/tools`](./tools) - `sync-agent`: promotes grounding + Voice Live config into the Foundry agent.
- [`/docs`](./docs) - spec, config schema, runbook, rehearsal checklist.

Design spec: `docs/superpowers/specs/2026-07-22-voice-live-avatar-design.md`.

> Deployment is a trusted operator machine with no web auth; the browser holds only a short-lived Entra token.
EOF
```

- [ ] **Step 3: Create empty solutions (proves toolchain)**

```bash
cd cli && dotnet new sln -n VoiceLive.Cli && cd ..
cd web && dotnet new sln -n VoiceLive.Web && cd ..
```

Expected: two `.sln` files created, no errors.

- [ ] **Step 4: Commit**

```bash
git add -A && git commit -m "chore: scaffold repo structure and solutions"
```

---

## Task 2: Config defaults + schema doc

**Files:**
- Create: `config/session.json`, `config/turntaking.json`, `config/agent.json`, `config/avatar.json`, `config/grounding/company-direction.md`, `docs/config-schema.md`

- [ ] **Step 1: Write config default files (valid JSON, Sweden Central + gpt-realtime)**

```bash
cd /home/jbergfeld/vcs/foundry-voice-live-avatar
cat > config/session.json <<'EOF'
{
  "endpoint": "wss://REPLACE-ME.services.ai.azure.com",
  "region": "swedencentral",
  "apiVersion": "2026-04-10",
  "model": "gpt-realtime",
  "voice": { "type": "azure-realtime-native", "name": "andrew" },
  "inputAudioSamplingRate": 24000,
  "inputAudioNoiseReduction": { "type": "azure_deep_noise_suppression" },
  "inputAudioEchoCancellation": { "type": "server_echo_cancellation" },
  "inputAudioTranscription": { "model": "azure-speech", "language": "en" }
}
EOF
cat > config/turntaking.json <<'EOF'
{
  "activeMode": "gated",
  "modes": {
    "open-mic": {
      "manualTurn": false,
      "turnDetection": {
        "type": "azure_semantic_vad",
        "threshold": 0.5,
        "prefixPaddingMs": 420,
        "silenceDurationMs": 500,
        "interruptResponse": true,
        "endOfUtteranceDetection": { "model": "semantic_detection_v1", "thresholdLevel": "medium", "timeoutMs": 1000 }
      }
    },
    "gated": { "manualTurn": true, "interruptResponse": false },
    "hybrid": {
      "manualTurn": false,
      "gateGatesBargeIn": true,
      "turnDetection": {
        "type": "azure_semantic_vad",
        "threshold": 0.5,
        "silenceDurationMs": 500,
        "interruptResponse": true,
        "endOfUtteranceDetection": { "model": "semantic_detection_v1", "thresholdLevel": "medium", "timeoutMs": 1000 }
      }
    }
  }
}
EOF
cat > config/agent.json <<'EOF'
{
  "agentName": "company-direction-avatar",
  "agentProjectName": "voice-live-avatar",
  "agentVersion": null,
  "conversationResumePolicy": "resume",
  "groundingStrategy": "pack",
  "safeQuestions": [
    "Let's refocus - what is our single most important priority this year?",
    "What does this direction mean for our customers?"
  ]
}
EOF
cat > config/avatar.json <<'EOF'
{
  "character": "lisa",
  "style": "casual-sitting",
  "customized": false,
  "video": { "resolution": { "width": 1920, "height": 1080 }, "bitrate": 2000000, "codec": "h264" }
}
EOF
cat > config/grounding/company-direction.md <<'EOF'
# Company Direction (grounding pack)

> Source-of-truth narrative. Synced into the Foundry agent's instructions by `tools/sync-agent`.

## Who you are
You are the on-stage avatar assistant for our company all-hands. You speak concisely and warmly.

## Our direction (placeholder - replace before rehearsal)
- Priority 1: ...
- Priority 2: ...
- Priority 3: ...

## Guardrails
- If a question drifts off-topic or is sensitive, gently redirect to company direction.
- Keep answers under ~30 seconds of speech unless asked to elaborate.
EOF
```

- [ ] **Step 2: Write `docs/config-schema.md`** documenting every field, type, required/optional, and allowed values for all four files (mirror the JSON above; note `session.json.voice.type ∈ {azure-realtime-native, azure-standard, azure-custom, openai}`, `turntaking.activeMode ∈ {open-mic, gated, hybrid}`, `agent.groundingStrategy ∈ {pack, rag, both}`, `agent.conversationResumePolicy ∈ {resume, fresh}`). Include a "validation rules" section: required fields, and that unknown `activeMode`/`voice.type`/`groundingStrategy` values fail fast.

- [ ] **Step 3: Verify JSON parses**

```bash
for f in config/*.json; do node -e "JSON.parse(require('fs').readFileSync('$f','utf8')); console.log('ok $f')"; done
```

Expected: `ok config/agent.json` ... for all four.

- [ ] **Step 4: Commit**

```bash
git add -A && git commit -m "feat(config): add default config files and schema doc"
```

---

## Task 3: CLI project + config model types

**Files:**
- Create: `cli/src/VoiceLive.Cli/VoiceLive.Cli.csproj`, `cli/src/VoiceLive.Cli/Config/ConfigModels.cs`
- Create: `cli/tests/VoiceLive.Cli.Tests/VoiceLive.Cli.Tests.csproj`

- [ ] **Step 1: Scaffold projects and add to solution**

```bash
cd /home/jbergfeld/vcs/foundry-voice-live-avatar/cli
dotnet new console -n VoiceLive.Cli -o src/VoiceLive.Cli -f net10.0
dotnet new xunit -n VoiceLive.Cli.Tests -o tests/VoiceLive.Cli.Tests -f net10.0
dotnet sln add src/VoiceLive.Cli/VoiceLive.Cli.csproj tests/VoiceLive.Cli.Tests/VoiceLive.Cli.Tests.csproj
dotnet add tests/VoiceLive.Cli.Tests/VoiceLive.Cli.Tests.csproj reference src/VoiceLive.Cli/VoiceLive.Cli.csproj
```

- [ ] **Step 2: Write `Config/ConfigModels.cs`** - records matching the JSON (camelCase → default STJ). The MVP CLI has **no Azure package dependencies** (Azure.AI.VoiceLive/Azure.Identity are added in Phase 7a where they are actually used), so it builds fully offline. Include:

```csharp
namespace VoiceLive.Cli.Config;

public sealed record VoiceConfig(string Type, string Name, double? Temperature = null, string? Rate = null, string? Style = null);
public sealed record NoiseReduction(string Type);
public sealed record EchoCancellation(string Type);
public sealed record Transcription(string Model, string? Language = null);

public sealed record SessionConfig(
    string Endpoint,
    string Region,
    string ApiVersion,
    string Model,
    VoiceConfig Voice,
    int InputAudioSamplingRate,
    NoiseReduction InputAudioNoiseReduction,
    EchoCancellation InputAudioEchoCancellation,
    Transcription InputAudioTranscription);

public sealed record EouDetection(string Model, string? ThresholdLevel = null, int? TimeoutMs = null);
public sealed record TurnDetectionConfig(
    string Type,
    double? Threshold = null,
    int? PrefixPaddingMs = null,
    int? SilenceDurationMs = null,
    bool? InterruptResponse = null,
    EouDetection? EndOfUtteranceDetection = null);

public sealed record TurnMode(
    bool ManualTurn = false,
    bool? GateGatesBargeIn = null,
    bool? InterruptResponse = null,
    TurnDetectionConfig? TurnDetection = null);

public sealed record TurnTakingConfig(string ActiveMode, Dictionary<string, TurnMode> Modes);

public sealed record AgentConfig(
    string AgentName,
    string AgentProjectName,
    string? AgentVersion,
    string ConversationResumePolicy,
    string GroundingStrategy,
    IReadOnlyList<string> SafeQuestions);
```

- [ ] **Step 3: Build**

```bash
cd /home/jbergfeld/vcs/foundry-voice-live-avatar/cli && dotnet build
```

Expected: Build succeeded.

- [ ] **Step 4: Commit**

```bash
git add -A && git commit -m "feat(cli): add project and config model types"
```

---

## Task 4: CLI config loader + fail-fast validator (TDD)

**Files:**
- Create: `cli/src/VoiceLive.Cli/Config/ConfigLoader.cs`, `cli/src/VoiceLive.Cli/Config/ConfigValidationException.cs`
- Test: `cli/tests/VoiceLive.Cli.Tests/ConfigLoaderTests.cs`

- [ ] **Step 1: Write the failing tests**

```csharp
using VoiceLive.Cli.Config;
using Xunit;

public class ConfigLoaderTests
{
    private static string WriteTemp(Dictionary<string,string> files)
    {
        var dir = Directory.CreateTempSubdirectory("vlcfg").FullName;
        Directory.CreateDirectory(Path.Combine(dir, "grounding"));
        foreach (var (name, content) in files) File.WriteAllText(Path.Combine(dir, name), content);
        return dir;
    }

    private static Dictionary<string,string> Valid() => new()
    {
        ["session.json"] = """
        {"endpoint":"wss://x.services.ai.azure.com","region":"swedencentral","apiVersion":"2026-04-10","model":"gpt-realtime",
         "voice":{"type":"azure-realtime-native","name":"andrew"},"inputAudioSamplingRate":24000,
         "inputAudioNoiseReduction":{"type":"azure_deep_noise_suppression"},
         "inputAudioEchoCancellation":{"type":"server_echo_cancellation"},
         "inputAudioTranscription":{"model":"azure-speech","language":"en"}}
        """,
        ["turntaking.json"] = """
        {"activeMode":"gated","modes":{"gated":{"manualTurn":true,"interruptResponse":false}}}
        """,
        ["agent.json"] = """
        {"agentName":"a","agentProjectName":"p","agentVersion":null,"conversationResumePolicy":"resume",
         "groundingStrategy":"pack","safeQuestions":["q1"]}
        """,
        ["avatar.json"] = """
        {"character":"lisa","style":"casual-sitting","customized":false,
         "video":{"resolution":{"width":1920,"height":1080},"bitrate":2000000,"codec":"h264"}}
        """
    };

    [Fact]
    public void Loads_valid_config()
    {
        var dir = WriteTemp(Valid());
        var cfg = ConfigLoader.Load(dir);
        Assert.Equal("swedencentral", cfg.Session.Region);
        Assert.Equal("gated", cfg.TurnTaking.ActiveMode);
        Assert.Equal("a", cfg.Agent.AgentName);
    }

    [Fact]
    public void Fails_when_active_mode_missing_from_modes()
    {
        var files = Valid();
        files["turntaking.json"] = """{"activeMode":"open-mic","modes":{"gated":{"manualTurn":true}}}""";
        var ex = Assert.Throws<ConfigValidationException>(() => ConfigLoader.Load(WriteTemp(files)));
        Assert.Contains("turntaking.json", ex.Message);
        Assert.Contains("open-mic", ex.Message);
    }

    [Fact]
    public void Fails_on_unknown_voice_type()
    {
        var files = Valid();
        files["session.json"] = files["session.json"].Replace("azure-realtime-native", "bogus-voice");
        var ex = Assert.Throws<ConfigValidationException>(() => ConfigLoader.Load(WriteTemp(files)));
        Assert.Contains("session.json", ex.Message);
        Assert.Contains("voice.type", ex.Message);
    }

    [Fact]
    public void Fails_with_missing_file_naming_the_file()
    {
        var files = Valid(); files.Remove("agent.json");
        var ex = Assert.Throws<ConfigValidationException>(() => ConfigLoader.Load(WriteTemp(files)));
        Assert.Contains("agent.json", ex.Message);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `cd cli && dotnet test`
Expected: FAIL (ConfigLoader/ConfigValidationException not defined).

- [ ] **Step 3: Implement `ConfigValidationException.cs` and `ConfigLoader.cs`**

```csharp
namespace VoiceLive.Cli.Config;

public sealed class ConfigValidationException(string message) : Exception(message);
```

```csharp
using System.Text.Json;

namespace VoiceLive.Cli.Config;

public sealed record AppConfig(SessionConfig Session, TurnTakingConfig TurnTaking, AgentConfig Agent);

public static class ConfigLoader
{
    private static readonly JsonSerializerOptions Opts = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    private static readonly string[] VoiceTypes = ["azure-realtime-native", "azure-standard", "azure-custom", "openai"];
    private static readonly string[] Modes = ["open-mic", "gated", "hybrid"];
    private static readonly string[] Grounding = ["pack", "rag", "both"];
    private static readonly string[] ResumePolicies = ["resume", "fresh"];

    public static AppConfig Load(string dir)
    {
        var errors = new List<string>();
        var session = Read<SessionConfig>(dir, "session.json", errors);
        var turn = Read<TurnTakingConfig>(dir, "turntaking.json", errors);
        var agent = Read<AgentConfig>(dir, "agent.json", errors);

        if (session is not null)
        {
            if (string.IsNullOrWhiteSpace(session.Endpoint)) errors.Add("session.json: endpoint is required");
            if (session.Voice is null || string.IsNullOrWhiteSpace(session.Voice.Type)) errors.Add("session.json: voice.type is required");
            else if (!VoiceTypes.Contains(session.Voice.Type)) errors.Add($"session.json: voice.type '{session.Voice.Type}' is not one of {string.Join(", ", VoiceTypes)}");
        }
        if (turn is not null)
        {
            if (!Modes.Contains(turn.ActiveMode)) errors.Add($"turntaking.json: activeMode '{turn.ActiveMode}' is not one of {string.Join(", ", Modes)}");
            else if (turn.Modes is null || !turn.Modes.ContainsKey(turn.ActiveMode)) errors.Add($"turntaking.json: activeMode '{turn.ActiveMode}' has no matching entry in modes");
        }
        if (agent is not null)
        {
            if (!Grounding.Contains(agent.GroundingStrategy)) errors.Add($"agent.json: groundingStrategy '{agent.GroundingStrategy}' is not one of {string.Join(", ", Grounding)}");
            if (!ResumePolicies.Contains(agent.ConversationResumePolicy)) errors.Add($"agent.json: conversationResumePolicy '{agent.ConversationResumePolicy}' is not one of {string.Join(", ", ResumePolicies)}");
        }

        if (errors.Count > 0)
            throw new ConfigValidationException("Configuration is invalid:\n  - " + string.Join("\n  - ", errors));

        return new AppConfig(session!, turn!, agent!);
    }

    private static T? Read<T>(string dir, string file, List<string> errors) where T : class
    {
        var path = Path.Combine(dir, file);
        if (!File.Exists(path)) { errors.Add($"{file}: file not found at {path}"); return null; }
        try { return JsonSerializer.Deserialize<T>(File.ReadAllText(path), Opts) ?? throw new JsonException("null document"); }
        catch (JsonException ex) { errors.Add($"{file}: invalid JSON - {ex.Message}"); return null; }
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `cd cli && dotnet test`
Expected: PASS (4 tests).

- [ ] **Step 5: Commit**

```bash
git add -A && git commit -m "feat(cli): config loader with fail-fast validation"
```

---

## Task 5: Turn-taking → `turn_detection` mapping (TDD)

**Files:**
- Create: `cli/src/VoiceLive.Cli/Session/SessionPayloadBuilder.cs`
- Test: `cli/tests/VoiceLive.Cli.Tests/SessionPayloadBuilderTests.cs`

The builder produces the `session` object for a `session.update` message, as a `Dictionary<string, object?>` serialized with **snake_case** wire names. `gated` mode omits `turn_detection` (manual turns); `open-mic`/`hybrid` include it.

- [ ] **Step 1: Write the failing tests**

```csharp
using System.Text.Json;
using VoiceLive.Cli.Config;
using VoiceLive.Cli.Session;
using Xunit;

public class SessionPayloadBuilderTests
{
    private static AppConfig Cfg(string activeMode, TurnMode mode)
    {
        var session = new SessionConfig("wss://x","swedencentral","2026-04-10","gpt-realtime",
            new VoiceConfig("azure-realtime-native","andrew"), 24000,
            new NoiseReduction("azure_deep_noise_suppression"),
            new EchoCancellation("server_echo_cancellation"),
            new Transcription("azure-speech","en"));
        var turn = new TurnTakingConfig(activeMode, new() { [activeMode] = mode });
        var agent = new AgentConfig("a","p",null,"resume","pack", new[]{"q"});
        return new AppConfig(session, turn, agent);
    }

    private static JsonElement BuildJson(AppConfig cfg)
        => JsonSerializer.SerializeToElement(SessionPayloadBuilder.Build(cfg));

    [Fact]
    public void Gated_mode_has_no_turn_detection_and_uses_snake_case()
    {
        var json = BuildJson(Cfg("gated", new TurnMode(ManualTurn: true, InterruptResponse: false)));
        Assert.False(json.TryGetProperty("turn_detection", out _));
        Assert.Equal("azure_deep_noise_suppression", json.GetProperty("input_audio_noise_reduction").GetProperty("type").GetString());
        Assert.Equal("andrew", json.GetProperty("voice").GetProperty("name").GetString());
    }

    [Fact]
    public void Open_mic_maps_azure_semantic_vad_and_eou()
    {
        var mode = new TurnMode(TurnDetection: new TurnDetectionConfig(
            "azure_semantic_vad", Threshold: 0.5, SilenceDurationMs: 500, InterruptResponse: true,
            EndOfUtteranceDetection: new EouDetection("semantic_detection_v1","medium",1000)));
        var td = BuildJson(Cfg("open-mic", mode)).GetProperty("turn_detection");
        Assert.Equal("azure_semantic_vad", td.GetProperty("type").GetString());
        Assert.True(td.GetProperty("interrupt_response").GetBoolean());
        Assert.Equal("semantic_detection_v1", td.GetProperty("end_of_utterance_detection").GetProperty("model").GetString());
        Assert.Equal(1000, td.GetProperty("end_of_utterance_detection").GetProperty("timeout_ms").GetInt32());
    }
}
```

- [ ] **Step 2: Run to verify fail**

Run: `cd cli && dotnet test --filter SessionPayloadBuilderTests`
Expected: FAIL (SessionPayloadBuilder not defined).

- [ ] **Step 3: Implement `SessionPayloadBuilder.cs`**

```csharp
using VoiceLive.Cli.Config;

namespace VoiceLive.Cli.Session;

public static class SessionPayloadBuilder
{
    public static Dictionary<string, object?> Build(AppConfig cfg)
    {
        var s = cfg.Session;
        var payload = new Dictionary<string, object?>
        {
            ["modalities"] = new[] { "text", "audio" },
            ["voice"] = Prune(new Dictionary<string, object?>
            {
                ["type"] = s.Voice.Type, ["name"] = s.Voice.Name,
                ["temperature"] = s.Voice.Temperature, ["rate"] = s.Voice.Rate, ["style"] = s.Voice.Style
            }),
            ["input_audio_sampling_rate"] = s.InputAudioSamplingRate,
            ["input_audio_noise_reduction"] = new Dictionary<string, object?> { ["type"] = s.InputAudioNoiseReduction.Type },
            ["input_audio_echo_cancellation"] = new Dictionary<string, object?> { ["type"] = s.InputAudioEchoCancellation.Type },
            ["input_audio_transcription"] = Prune(new Dictionary<string, object?>
            {
                ["model"] = s.InputAudioTranscription.Model, ["language"] = s.InputAudioTranscription.Language
            })
        };

        var mode = cfg.TurnTaking.Modes[cfg.TurnTaking.ActiveMode];
        if (!mode.ManualTurn && mode.TurnDetection is { } td)
            payload["turn_detection"] = BuildTurnDetection(td);

        return payload;
    }

    private static Dictionary<string, object?> BuildTurnDetection(TurnDetectionConfig td)
    {
        var d = new Dictionary<string, object?>
        {
            ["type"] = td.Type,
            ["threshold"] = td.Threshold,
            ["prefix_padding_ms"] = td.PrefixPaddingMs,
            ["silence_duration_ms"] = td.SilenceDurationMs,
            ["interrupt_response"] = td.InterruptResponse
        };
        if (td.EndOfUtteranceDetection is { } e)
            d["end_of_utterance_detection"] = Prune(new Dictionary<string, object?>
            {
                ["model"] = e.Model, ["threshold_level"] = e.ThresholdLevel, ["timeout_ms"] = e.TimeoutMs
            });
        return Prune(d);
    }

    private static Dictionary<string, object?> Prune(Dictionary<string, object?> d)
        => d.Where(kv => kv.Value is not null).ToDictionary(kv => kv.Key, kv => kv.Value);
}
```

- [ ] **Step 4: Run to verify pass**

Run: `cd cli && dotnet test`
Expected: PASS (all tests).

- [ ] **Step 5: Commit**

```bash
git add -A && git commit -m "feat(cli): session payload builder with turn-taking mapping"
```

---

## Task 6: CLI `validate` command

**Files:**
- Modify: `cli/src/VoiceLive.Cli/Program.cs`
- Test: `cli/tests/VoiceLive.Cli.Tests/ValidateCommandTests.cs`

Hand-rolled arg parsing for MVP (avoid System.CommandLine prerelease churn; add it in Phase 7 when the command surface grows).

- [ ] **Step 1: Write `Program.cs`**

```csharp
using System.Text.Json;
using VoiceLive.Cli.Config;
using VoiceLive.Cli.Session;

namespace VoiceLive.Cli;

public static class Program
{
    public static int Main(string[] args) => Run(args, Console.Out, Console.Error);

    public static int Run(string[] args, TextWriter outw, TextWriter errw)
    {
        if (args.Length == 0 || args[0] != "validate")
        {
            errw.WriteLine("usage: voicelive-cli validate --config <dir>");
            return 2;
        }
        var dir = ArgValue(args, "--config") ?? "config";
        try
        {
            var cfg = ConfigLoader.Load(dir);
            var payload = SessionPayloadBuilder.Build(cfg);
            outw.WriteLine($"Config OK. Active turn-taking mode: {cfg.TurnTaking.ActiveMode}");
            outw.WriteLine("Resolved session.update payload:");
            outw.WriteLine(JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true }));
            return 0;
        }
        catch (ConfigValidationException ex)
        {
            errw.WriteLine(ex.Message);
            return 1;
        }
    }

    private static string? ArgValue(string[] args, string name)
    {
        var i = Array.IndexOf(args, name);
        return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
    }
}
```

- [ ] **Step 2: Write the failing test**

```csharp
using VoiceLive.Cli;
using Xunit;

public class ValidateCommandTests
{
    [Fact]
    public void Validate_on_repo_config_returns_zero()
    {
        var repoConfig = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "..", "config");
        var outw = new StringWriter(); var errw = new StringWriter();
        var code = Program.Run(["validate", "--config", repoConfig], outw, errw);
        Assert.Equal(0, code);
        Assert.Contains("Config OK", outw.ToString());
    }

    [Fact]
    public void Validate_on_bad_dir_returns_one()
    {
        var outw = new StringWriter(); var errw = new StringWriter();
        var code = Program.Run(["validate", "--config", "/no/such/dir"], outw, errw);
        Assert.Equal(1, code);
        Assert.Contains("session.json", errw.ToString());
    }
}
```

- [ ] **Step 3: Run to verify pass after Program.cs exists**

Run: `cd cli && dotnet test`
Expected: PASS. (The first test reads the real `/config` via relative path from the test bin dir; adjust the number of `..` segments if the path differs.)

- [ ] **Step 4: Manual smoke run**

```bash
cd /home/jbergfeld/vcs/foundry-voice-live-avatar/cli/src/VoiceLive.Cli
dotnet run -- validate --config ../../../config
```

Expected: "Config OK. Active turn-taking mode: gated" + a JSON payload with no `turn_detection` key.

- [ ] **Step 5: Commit**

```bash
cd /home/jbergfeld/vcs/foundry-voice-live-avatar
git add -A && git commit -m "feat(cli): add validate command"
```

---

## Task 7: CLI README

**Files:**
- Create: `cli/README.md`

- [ ] **Step 1: Write `cli/README.md`** describing: purpose (rehearsal harness), Windows requirement for audio (NAudio), the `validate` command, planned commands (`run`, `sync-agent`), and how config hot-reload will work (smart reload). Explicitly state audio/live-session is Phase 7.

- [ ] **Step 2: Commit**

```bash
git add -A && git commit -m "docs(cli): add CLI readme"
```

---

## Task 8: Web backend project + `/api/config` + `/api/health` (TDD)

**Files:**
- Create: `web/src/VoiceLive.Web/VoiceLive.Web.csproj`, `web/src/VoiceLive.Web/Config/WebConfig.cs`, `web/src/VoiceLive.Web/Program.cs`
- Create: `web/tests/VoiceLive.Web.Tests/VoiceLive.Web.Tests.csproj`, `web/tests/VoiceLive.Web.Tests/ConfigEndpointTests.cs`

The web app owns its **own** config models/loader (duplicated from the CLI on purpose). For MVP it loads `session.json`, `turntaking.json`, `agent.json`, `avatar.json` and exposes a sanitized subset to the browser.

- [ ] **Step 1: Scaffold**

```bash
cd /home/jbergfeld/vcs/foundry-voice-live-avatar/web
dotnet new web -n VoiceLive.Web -o src/VoiceLive.Web -f net10.0
dotnet new xunit -n VoiceLive.Web.Tests -o tests/VoiceLive.Web.Tests -f net10.0
dotnet sln add src/VoiceLive.Web/VoiceLive.Web.csproj tests/VoiceLive.Web.Tests/VoiceLive.Web.Tests.csproj
dotnet add tests/VoiceLive.Web.Tests/VoiceLive.Web.Tests.csproj reference src/VoiceLive.Web/VoiceLive.Web.csproj
dotnet add tests/VoiceLive.Web.Tests/VoiceLive.Web.Tests.csproj package Microsoft.AspNetCore.Mvc.Testing
dotnet add src/VoiceLive.Web/VoiceLive.Web.csproj package Azure.Identity
```

- [ ] **Step 2: Write `Config/WebConfig.cs`** - a `WebConfigLoader.Load(dir)` returning a `record ClientConfig` containing only browser-safe fields: `region`, `apiVersion`, `model`, `voice`, `avatar`, `activeMode`, `agentName`, `agentProjectName`, `safeQuestions`. It must **not** include the endpoint secret or credentials. Reuse the same validation approach as CLI Task 4 (fail fast with file/field messages; throw `WebConfigValidationException`). The config dir comes from `builder.Configuration["ConfigDir"]` (default `config`).

- [ ] **Step 3: Write `Program.cs`** exposing:
  - `GET /api/health` → `200 {"status":"ok"}`
  - `GET /api/config` → 200 with `ClientConfig` JSON, or `500 {"error": "<message>"}` when config invalid (fail clearly, do not serve partial/fake config).
  - Static files from `wwwroot` (`app.UseDefaultFiles(); app.UseStaticFiles();`).
  - Add `public partial class Program { }` at the bottom so `WebApplicationFactory<Program>` works.
  - Read `ConfigDir` from configuration (default `config`) so tests can override via `UseSetting`.

- [ ] **Step 4: Write the failing tests**

```csharp
using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

public class ConfigEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    public ConfigEndpointTests(WebApplicationFactory<Program> f)
    {
        var repoConfig = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..","..","..","..","..","..","config"));
        _factory = f.WithWebHostBuilder(b => b.UseSetting("ConfigDir", repoConfig));
    }

    [Fact]
    public async Task Health_returns_ok()
    {
        var res = await _factory.CreateClient().GetAsync("/api/health");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }

    [Fact]
    public async Task Config_returns_sanitized_client_config()
    {
        var res = await _factory.CreateClient().GetAsync("/api/config");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var body = await res.Content.ReadAsStringAsync();
        Assert.Contains("swedencentral", body);
        Assert.DoesNotContain("services.ai.azure.com", body); // endpoint must not leak to the browser
    }
}
```

- [ ] **Step 5: Run tests**

Run: `cd web && dotnet test`
Expected: PASS. Fix relative-path depth in the test if needed until `swedencentral` is found (adjust the number of `..`).

- [ ] **Step 6: Commit**

```bash
git add -A && git commit -m "feat(web): backend with /api/config and /api/health"
```

---

## Task 9: Token broker `/api/token` (fails clearly without Azure) (TDD)

**Files:**
- Create: `web/src/VoiceLive.Web/Tokens/ITokenBroker.cs`, `web/src/VoiceLive.Web/Tokens/TokenBrokerException.cs`, `web/src/VoiceLive.Web/Tokens/EntraTokenBroker.cs`
- Modify: `web/src/VoiceLive.Web/Program.cs`
- Test: `web/tests/VoiceLive.Web.Tests/TokenEndpointTests.cs`

Design per the user's error-handling preference: **never mask a failure**. If token acquisition fails, `/api/token` returns HTTP 502 with an explicit message; it never returns a fake token.

- [ ] **Step 1: Write `ITokenBroker.cs` and `TokenBrokerException.cs`**

```csharp
namespace VoiceLive.Web.Tokens;

public sealed record AccessTokenResult(string Token, DateTimeOffset ExpiresOn);

public interface ITokenBroker
{
    Task<AccessTokenResult> GetTokenAsync(CancellationToken ct);
}
```

```csharp
namespace VoiceLive.Web.Tokens;

public sealed class TokenBrokerException(string message) : Exception(message);
```

- [ ] **Step 2: Write `EntraTokenBroker.cs`** using `DefaultAzureCredential` to fetch a token for scope `https://ai.azure.com/.default`. On `CredentialUnavailableException`/`AuthenticationFailedException`, wrap and rethrow as a `TokenBrokerException` with a clear message ("No Azure credential available; run `az login` or configure a managed identity").

- [ ] **Step 3: Wire `/api/token` in `Program.cs`**: resolve `ITokenBroker`; on success return `200 {token, expiresOn}`; catch `TokenBrokerException` → `502 {"error": message}`. Register `EntraTokenBroker` as the default `ITokenBroker` (tests override it).

- [ ] **Step 4: Write the failing tests**

```csharp
using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using VoiceLive.Web.Tokens;
using Xunit;

public class TokenEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    public TokenEndpointTests(WebApplicationFactory<Program> f) => _factory = f;

    private sealed class FakeOk : ITokenBroker
    { public Task<AccessTokenResult> GetTokenAsync(CancellationToken ct)
        => Task.FromResult(new AccessTokenResult("faketoken", DateTimeOffset.UtcNow.AddMinutes(30))); }

    private sealed class FakeFail : ITokenBroker
    { public Task<AccessTokenResult> GetTokenAsync(CancellationToken ct)
        => throw new TokenBrokerException("No Azure credential available"); }

    [Fact]
    public async Task Returns_token_when_broker_ok()
    {
        var client = _factory.WithWebHostBuilder(b => b.ConfigureServices(s =>
            s.AddSingleton<ITokenBroker, FakeOk>())).CreateClient();
        var res = await client.GetAsync("/api/token");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.Contains("faketoken", await res.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Returns_502_with_clear_message_when_no_credential()
    {
        var client = _factory.WithWebHostBuilder(b => b.ConfigureServices(s =>
            s.AddSingleton<ITokenBroker, FakeFail>())).CreateClient();
        var res = await client.GetAsync("/api/token");
        Assert.Equal(HttpStatusCode.BadGateway, res.StatusCode);
        Assert.Contains("credential", await res.Content.ReadAsStringAsync());
    }
}
```

- [ ] **Step 5: Run tests**

Run: `cd web && dotnet test`
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add -A && git commit -m "feat(web): token broker endpoint that fails clearly without Azure"
```

---

## Task 10: Web README + run smoke

**Files:**
- Create: `web/README.md`

- [ ] **Step 1: Write `web/README.md`** - architecture (token broker + config + static), endpoints, how to run (`dotnet run` then curl), security note (short-lived token only), and that avatar/session run in the browser (Phase 7 / frontend).

- [ ] **Step 2: Smoke run**

```bash
cd /home/jbergfeld/vcs/foundry-voice-live-avatar/web/src/VoiceLive.Web
ConfigDir=../../../config ASPNETCORE_URLS=http://localhost:5280 dotnet run &
sleep 8
curl -s localhost:5280/api/health; echo
curl -s localhost:5280/api/config | head -c 300; echo
kill "$(pgrep -f VoiceLive.Web | head -1)"
```

Expected: health `{"status":"ok"}`; config JSON containing `swedencentral` and no endpoint host.

- [ ] **Step 3: Commit**

```bash
cd /home/jbergfeld/vcs/foundry-voice-live-avatar
git add -A && git commit -m "docs(web): add web readme"
```

---

## Task 11: Frontend scaffold (vanilla TS + esbuild)

**Files:**
- Create: `web/frontend/package.json`, `web/frontend/tsconfig.json`, `web/frontend/src/main.ts`, `web/frontend/src/views.ts`, `web/src/VoiceLive.Web/wwwroot/index.html`

- [ ] **Step 1: Init frontend and add esbuild + typescript**

```bash
cd /home/jbergfeld/vcs/foundry-voice-live-avatar/web/frontend
npm init -y
npm install -D esbuild typescript
```

- [ ] **Step 2: Add build script to `package.json`**: `"build": "esbuild src/main.ts --bundle --format=esm --outfile=../src/VoiceLive.Web/wwwroot/app.js"`. Add a minimal `tsconfig.json` (target ES2022, module ESNext, strict true, moduleResolution bundler).

- [ ] **Step 3: Write `src/views.ts`** - two exported functions `renderOperatorView(root, cfg)` and `renderDisplayView(root)` that build: the operator control shell (device select placeholder, mic-gate button, mode label, panic buttons: Stop / Repeat / Safe question) and the fullscreen `<video id="avatar">` display. No SDK yet.

- [ ] **Step 4: Write `src/main.ts`**

```ts
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
```

- [ ] **Step 5: Write `wwwroot/index.html`** loading `app.js` as a module (`<script type="module" src="/app.js"></script>`) with a `<div id="app"></div>` and minimal CSS (display view = black bg, fullscreen video). Place it at `web/src/VoiceLive.Web/wwwroot/index.html`.

- [ ] **Step 6: Build**

```bash
cd /home/jbergfeld/vcs/foundry-voice-live-avatar/web/frontend && npm run build
```

Expected: `../src/VoiceLive.Web/wwwroot/app.js` created, no errors. (`main.ts` statically imports `./views`; esbuild resolves the extensionless import to `views.ts` and bundles both into `app.js`.)

- [ ] **Step 7: Commit**

```bash
cd /home/jbergfeld/vcs/foundry-voice-live-avatar
git add -A && git commit -m "feat(web): vanilla TS frontend scaffold with operator/display views"
```

---

## Task 12: CI pipeline (build + test both apps independently)

**Files:**
- Create: `pipeline/ci.yml`

- [ ] **Step 1: Write `pipeline/ci.yml`** - a GitHub Actions workflow (`on: [push, pull_request]`) with three independent jobs, no cross-job `needs`:
  - `cli`: `actions/setup-dotnet@v4` (`dotnet-version: 10.0.x`) → `dotnet test cli/VoiceLive.Cli.sln`
  - `web`: `actions/setup-dotnet@v4` → `dotnet test web/VoiceLive.Web.sln`
  - `frontend`: `actions/setup-node@v4` (`node-version: 24`) → `npm install` + `npm run build` in `web/frontend`

- [ ] **Step 2: Lint the workflow with the locally available actionlint**

```bash
~/.local/bin/actionlint pipeline/ci.yml
```

Expected: no errors.

- [ ] **Step 3: Commit**

```bash
git add -A && git commit -m "ci: build and test cli, web, and frontend independently"
```

---

## Task 13: Full MVP verification gate

- [ ] **Step 1: Run everything green**

```bash
cd /home/jbergfeld/vcs/foundry-voice-live-avatar
dotnet test cli/VoiceLive.Cli.sln && dotnet test web/VoiceLive.Web.sln && (cd web/frontend && npm run build)
cd cli/src/VoiceLive.Cli && dotnet run -- validate --config ../../../config
```

Expected: all tests pass; validate prints "Config OK" + payload.

- [ ] **Step 2: Commit any fixes**

```bash
cd /home/jbergfeld/vcs/foundry-voice-live-avatar
git add -A && git commit -m "chore: MVP verification green" --allow-empty
```

---

## Phase 7 — Live Azure wiring (POST-MVP, requires provisioned Foundry resource + credentials)

**Do not fake any Azure/SDK calls.** These tasks must be written against the actually-compiled SDK API and a real resource. They are outlined here (not fully coded) precisely because the exact `Azure.AI.VoiceLive` / `@azure/ai-voicelive` method surface must be confirmed by compiling against the installed package, and no live endpoint may be invented.

- **7a. CLI live session:** wire `VoiceLiveClient` + `AgentSessionConfig(agentName, projectName)` (Entra via `DefaultAzureCredential`), NAudio capture/playback (Windows), transcript printing from `response.audio_transcript.*` + `input_audio_transcription.*`, and latency measurement (`input_audio_buffer.speech_stopped`/EOU → first `response.audio.delta`). Add `run` and `sync-agent` commands (adopt `System.CommandLine` here). Verify on the Windows host.
- **7b. Web live session:** in the browser, `import { VoiceLiveClient } from "@azure/ai-voicelive"`, fetch `/api/token`, open an agent-mode session, apply `/api/config`, run audio, and negotiate avatar WebRTC (`session.avatar.connect {client_sdp}` → `session.avatar.connecting {server_sdp}`; ICE from `session.updated`). Implement panic controls, barge-in (`response.cancel` + `output_audio_buffer.clear`), reconnect via `ConversationId`, and token auto-refresh.
- **7c. `tools/sync-agent`:** read `/config/grounding/*.md` + Voice Live config and push into the Foundry agent (instructions + metadata `microsoft.voice-live.configuration`). Provide a `--dry-run` that prints intended changes without calling Azure.
- **7d. Provisioning + runbook:** document resource/agent creation (region `swedencentral`), RBAC (`Cognitive Services User` + `Foundry User`), and write `docs/runbook.md` + `docs/rehearsal-checklist.md`.

---

## Notes on deviations from the spec (intentional, MVP-scoped)

- **System.CommandLine** deferred to Phase 7 (hand-rolled parsing for the single `validate` command) to keep the MVP off a prerelease API.
- **Live audio & Voice Live session** deferred to Phase 7 - the MVP proves config→wire mapping and the token-broker/config surface without a provisioned resource, so it stays green on Linux and fakes nothing.
- The web app **duplicates** config models rather than sharing a library (spec §3.3).
