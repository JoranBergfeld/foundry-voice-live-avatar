# Web Agent Mode Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add an opt-in **agent mode** to the `/web` show client so it connects to the Foundry agent `company-direction-avatar` via `SessionTarget.FromAgent(...)`, enabling server-side hosted tool calling, while keeping avatar+voice working and surfacing tool-invocation events. Model mode stays the default.

**Architecture:** A resolved `mode` (`model`|`agent`) comes from `session.json` with a `VOICELIVE_MODE` env override. In `agent` mode the bridge starts the Voice Live session from the agent and configures it with a `BuildForAgent` options set that omits `Model`+`Instructions` but keeps voice/avatar/audio/turn-taking. Tool/function/MCP update events from the SDK are logged and forwarded to the browser for observability. Nothing depends on tools existing — the path is tool-agnostic.

**Tech Stack:** .NET 10, `Azure.AI.VoiceLive` 1.1.0, ASP.NET minimal API + WebSockets, xUnit; TypeScript frontend bundled by esbuild.

**Spec:** `docs/superpowers/specs/2026-07-22-web-agent-mode-design.md`

---

## File Structure

**Create:**
- `web/src/VoiceLive.Web/Config/SessionModeResolver.cs` — pure mode resolution/validation (`model`|`agent`, env-wins).
- `web/src/VoiceLive.Web/Session/ToolNotification.cs` — DTO for the browser `tool` frame (stable wire contract).
- `web/tests/VoiceLive.Web.Tests/SessionModeResolverTests.cs` — unit tests for resolution/validation.
- `web/tests/VoiceLive.Web.Tests/ToolNotificationTests.cs` — unit test for the tool-frame JSON shape.

**Modify:**
- `web/src/VoiceLive.Web/Config/ServerSessionConfig.cs` — add `Mode` to `ServerSessionConfig` + `ServerSessionFile`, validate + normalize in `LoadServerSession`.
- `web/src/VoiceLive.Web/Program.cs` — read `VOICELIVE_MODE` once, apply env override to the loaded config.
- `web/src/VoiceLive.Web/Session/SessionOptionsBuilder.cs` — extract `BuildCommon`, add `BuildForAgent`.
- `web/src/VoiceLive.Web/Session/VoiceLiveWebSocketBridge.cs` — branch on `config.Mode`; add `mode` to `ready`; add tool-event cases + `SendToolAsync`.
- `web/tests/VoiceLive.Web.Tests/ServerSessionConfigTests.cs` — assert default mode + `BuildForAgent` field retention.
- `config/session.json` — add `"mode": "model"`.
- `web/frontend/src/views.ts` — `ReadyConfig.mode`, session-mode line, `noteTool` + tools panel.
- `web/frontend/src/main.ts` — `tool` frame type + handler.
- `web/src/VoiceLive.Web/wwwroot/app.js` — regenerated bundle (build artifact, committed).
- `docs/config-schema.md`, `web/README.md` — document `mode` + `VOICELIVE_MODE`.

**Build/test commands (verified for this repo):**
- Web tests: `dotnet test web/VoiceLive.Web.sln`
- Frontend bundle: `cd web/frontend && npm run build` (emits `../src/VoiceLive.Web/wwwroot/app.js`)
- Run web locally: `ConfigDir=$PWD/config ASPNETCORE_URLS=http://127.0.0.1:5210 dotnet run --no-launch-profile --project web/src/VoiceLive.Web`

---

## Task 1: Gating spike — does agent mode accept avatar+voice via `session.update`?

This is a **live verification**, not a code change to the repo. It answers the one load-bearing unknown before we build the bridge branch: in agent mode, is a `session.update` carrying `Avatar`+`Voice` **accepted** (yields `SessionUpdateSessionUpdated` with `Avatar.IceServers`), or **rejected** like `Instructions` was (`instructions_configuration_not_supported`)? Requires `az login` (data-plane role on `testlab-f`).

**Files:** throwaway probe under `/tmp/agentavatarspike/` (NOT committed to the repo).

- [ ] **Step 1: Scaffold the throwaway probe**

```bash
mkdir -p /tmp/agentavatarspike && cd /tmp/agentavatarspike
cat > spike.csproj <<'EOF'
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Azure.AI.VoiceLive" Version="1.1.0" />
    <PackageReference Include="Azure.Identity" Version="1.13.1" />
  </ItemGroup>
</Project>
EOF
```

- [ ] **Step 2: Write the probe (agent mode + avatar/voice session.update)**

```bash
cat > Program.cs <<'EOF'
using Azure.AI.VoiceLive;
using Azure.Identity;

var endpoint = new Uri("https://testlab-f.services.ai.azure.com");
var client = new VoiceLiveClient(endpoint, new DefaultAzureCredential(), new VoiceLiveClientOptions(VoiceLiveClientOptions.ServiceVersion.V2025_10_01));

var agent = new AgentSessionConfig("company-direction-avatar", "proj-default");
await using var session = await client.StartSessionAsync(SessionTarget.FromAgent(agent));
Console.WriteLine("session started (agent mode)");

var options = new VoiceLiveSessionOptions
{
    // NOTE: no Model, no Instructions (agent owns them)
    Voice = new AzureStandardVoice("en-US-AndrewNeural"),
    TurnDetection = new NoTurnDetection(),
    InputAudioFormat = InputAudioFormat.Pcm16,
    OutputAudioFormat = OutputAudioFormat.Pcm16,
    InputAudioSamplingRate = 24000,
    Avatar = new AvatarConfiguration("lisa", false)
    {
        Style = "casual-sitting",
        Video = new VideoParams { Bitrate = 2000000, Codec = "h264", Resolution = new VideoResolution(1920, 1080) }
    },
};
options.Modalities.Clear();
options.Modalities.Add(InteractionModality.Text);
options.Modalities.Add(InteractionModality.Audio);

await session.ConfigureSessionAsync(options);
Console.WriteLine("session.update sent (with avatar+voice)");

using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
try
{
    await foreach (var update in session.GetUpdatesAsync(cts.Token))
    {
        switch (update)
        {
            case SessionUpdateSessionUpdated u:
                var ice = u.Session?.Avatar?.IceServers;
                Console.WriteLine($"RESULT: SessionUpdated. avatar.iceServers count = {(ice?.Count ?? 0)}");
                Console.WriteLine(ice is { Count: > 0 } ? "SPIKE PASS: agent mode ACCEPTED avatar+voice" : "SPIKE UNCLEAR: updated but no ICE servers");
                return;
            case SessionUpdateError e:
                Console.WriteLine($"RESULT: ERROR code={e.Error?.Code} type={e.Error?.Type} msg={e.Error?.Message}");
                Console.WriteLine("SPIKE FAIL: agent mode REJECTED the session.update (see message)");
                return;
        }
    }
    Console.WriteLine("SPIKE UNCLEAR: no SessionUpdated/Error within timeout");
}
catch (OperationCanceledException) { Console.WriteLine("SPIKE UNCLEAR: timed out"); }
EOF
```

- [ ] **Step 3: Run the probe and record the outcome**

Run: `cd /tmp/agentavatarspike && dotnet run -c Release`
Expected (one of):
- `SPIKE PASS: agent mode ACCEPTED avatar+voice` → **proceed with Tasks 2–10 as written**.
- `SPIKE FAIL: ...REJECTED...` (e.g. an `avatar`/`voice` `*_configuration_not_supported`) → **STOP**. Record the exact error and report to the owner: the web cannot use agent mode without moving voice+avatar into the agent's voice-live metadata (deferred `sync-agent` work). Do not proceed past Task 1 without an owner decision.

- [ ] **Step 4: Record findings + clean up**

```bash
# Copy the console outcome into the plan's Task 1 as a note (PASS/FAIL + any error text), then:
rm -rf /tmp/agentavatarspike
```
If the spike changed platform understanding (e.g. avatar rejected), store a repository memory capturing the verified fact.

> **Gate:** Only continue to Task 2 if Step 3 was `SPIKE PASS`.

---

## Task 2: `SessionModeResolver` (pure resolution + validation)

**Files:**
- Create: `web/src/VoiceLive.Web/Config/SessionModeResolver.cs`
- Test: `web/tests/VoiceLive.Web.Tests/SessionModeResolverTests.cs`

- [ ] **Step 1: Write the failing tests**

```csharp
using VoiceLive.Web.Config;
using Xunit;

public class SessionModeResolverTests
{
    [Fact]
    public void Defaults_to_model_when_both_absent()
        => Assert.Equal("model", SessionModeResolver.Resolve(configMode: null, envMode: null));

    [Fact]
    public void Uses_config_mode_when_env_absent()
        => Assert.Equal("agent", SessionModeResolver.Resolve("agent", null));

    [Fact]
    public void Env_overrides_config()
        => Assert.Equal("model", SessionModeResolver.Resolve("agent", "model"));

    [Theory]
    [InlineData(" Agent ")]
    [InlineData("AGENT")]
    public void Normalizes_case_and_whitespace(string value)
        => Assert.Equal("agent", SessionModeResolver.Resolve(value, null));

    [Fact]
    public void Invalid_config_mode_throws()
    {
        var ex = Assert.Throws<WebConfigValidationException>(() => SessionModeResolver.Resolve("hybrid", null));
        Assert.Contains("hybrid", ex.Message);
        Assert.Contains("model", ex.Message);
        Assert.Contains("agent", ex.Message);
    }

    [Fact]
    public void Invalid_env_mode_throws()
        => Assert.Throws<WebConfigValidationException>(() => SessionModeResolver.Resolve(null, "bogus"));

    [Fact]
    public void IsValid_and_Normalize_behave()
    {
        Assert.True(SessionModeResolver.IsValid("Agent"));
        Assert.False(SessionModeResolver.IsValid("nope"));
        Assert.Equal("model", SessionModeResolver.Normalize(null));
        Assert.Equal("agent", SessionModeResolver.Normalize(" AGENT "));
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test web/VoiceLive.Web.sln --filter FullyQualifiedName~SessionModeResolverTests`
Expected: FAIL — `SessionModeResolver` does not exist (compile error).

- [ ] **Step 3: Implement `SessionModeResolver`**

```csharp
namespace VoiceLive.Web.Config;

public static class SessionModeResolver
{
    public const string Model = "model";
    public const string Agent = "agent";

    public static bool IsValid(string? value)
        => value is not null && Normalize(value) is Model or Agent;

    public static string Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? Model : value.Trim().ToLowerInvariant();

    /// <summary>Env override wins over the config value; both are validated. Invalid values throw.</summary>
    public static string Resolve(string? configMode, string? envMode)
    {
        var chosen = !string.IsNullOrWhiteSpace(envMode) ? envMode : configMode;
        if (string.IsNullOrWhiteSpace(chosen)) return Model;
        if (!IsValid(chosen))
            throw new WebConfigValidationException(
                $"session mode '{chosen}' is invalid; expected '{Model}' or '{Agent}' (from session.json 'mode' or VOICELIVE_MODE).");
        return Normalize(chosen);
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test web/VoiceLive.Web.sln --filter FullyQualifiedName~SessionModeResolverTests`
Expected: PASS (8 tests).

- [ ] **Step 5: Commit**

```bash
git add web/src/VoiceLive.Web/Config/SessionModeResolver.cs web/tests/VoiceLive.Web.Tests/SessionModeResolverTests.cs
git commit -m "feat(web): add SessionModeResolver for model/agent mode selection

Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>
Copilot-Session: 3ec69cc9-32a0-41ae-84df-c1ff016b1860"
```

---

## Task 3: Carry `Mode` on the server config

**Files:**
- Modify: `web/src/VoiceLive.Web/Config/ServerSessionConfig.cs`
- Modify: `config/session.json`
- Test: `web/tests/VoiceLive.Web.Tests/ServerSessionConfigTests.cs`

- [ ] **Step 1: Write the failing test (append to `ServerSessionConfigTests`)**

```csharp
    [Fact]
    public void LoadServerSession_defaults_mode_to_model()
    {
        var config = WebConfigLoader.LoadServerSession(RepoConfigDir);
        Assert.Equal("model", config.Mode);
    }
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test web/VoiceLive.Web.sln --filter FullyQualifiedName~LoadServerSession_defaults_mode_to_model`
Expected: FAIL — `ServerSessionConfig` has no `Mode` member (compile error).

- [ ] **Step 3: Add `Mode` to the record + file, validate + normalize in the loader**

In `ServerSessionConfig.cs`, add `string Mode` as the final positional parameter of `ServerSessionConfig`:

```csharp
public sealed record ServerSessionConfig(
    string Endpoint,
    string Region,
    string ApiVersion,
    string Model,
    ServerVoiceConfig Voice,
    int InputAudioSamplingRate,
    ServerNoiseReductionConfig? InputAudioNoiseReduction,
    ServerEchoCancellationConfig? InputAudioEchoCancellation,
    ServerTranscriptionConfig? InputAudioTranscription,
    ServerTurnTakingConfig TurnTaking,
    ServerAvatarConfig Avatar,
    ServerAgentConfig Agent,
    string Mode);
```

Add `Mode` to the private file record `ServerSessionFile`:

```csharp
    private sealed record ServerSessionFile(
        string? Endpoint,
        string? Region,
        string? ApiVersion,
        string? Model,
        ServerVoiceFile? Voice,
        int InputAudioSamplingRate,
        ServerNoiseReductionFile? InputAudioNoiseReduction,
        ServerEchoCancellationFile? InputAudioEchoCancellation,
        ServerTranscriptionFile? InputAudioTranscription,
        string? Mode);
```

In `LoadServerSession`, inside the `if (session is not null)` validation block, add a mode check:

```csharp
            if (session.Mode is not null && !SessionModeResolver.IsValid(session.Mode))
                errors.Add($"session.json: mode: '{session.Mode}' is not one of {SessionModeResolver.Model}, {SessionModeResolver.Agent}");
```

Then pass `Mode` into the constructor call (last argument of the `return new ServerSessionConfig(...)`):

```csharp
            new ServerAgentConfig(agent!.AgentName!, agent.AgentProjectName!, agent.SafeQuestions!),
            SessionModeResolver.Normalize(session!.Mode));
```

- [ ] **Step 4: Add `"mode": "model"` to `config/session.json`**

```json
{
  "endpoint": "https://testlab-f.services.ai.azure.com",
  "region": "swedencentral",
  "apiVersion": "2025-10-01",
  "mode": "model",
  "model": "gpt-realtime",
  "voice": { "type": "azure-realtime-native", "name": "en-US-AndrewNeural" },
  "inputAudioSamplingRate": 24000,
  "inputAudioNoiseReduction": { "type": "azure_deep_noise_suppression" },
  "inputAudioEchoCancellation": { "type": "server_echo_cancellation" },
  "inputAudioTranscription": { "model": "azure-speech", "language": "en" }
}
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test web/VoiceLive.Web.sln --filter FullyQualifiedName~ServerSessionConfigTests`
Expected: PASS (existing + new test).

- [ ] **Step 6: Commit**

```bash
git add web/src/VoiceLive.Web/Config/ServerSessionConfig.cs web/tests/VoiceLive.Web.Tests/ServerSessionConfigTests.cs config/session.json
git commit -m "feat(web): carry resolved session mode on ServerSessionConfig

Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>
Copilot-Session: 3ec69cc9-32a0-41ae-84df-c1ff016b1860"
```

---

## Task 4: Apply the `VOICELIVE_MODE` env override in `Program.cs`

**Files:**
- Modify: `web/src/VoiceLive.Web/Program.cs`

Env-override precedence is already unit-tested via `SessionModeResolver` (Task 2). This task wires it into request handling. There is no dedicated endpoint that echoes the mode, so this wiring is verified by the resolver tests + the live run (Task 10).

- [ ] **Step 1: Read the env value once, near the top of `Program.cs`**

Just after `var configDir = builder.Configuration["ConfigDir"] ?? "config";` add:

```csharp
var envSessionMode = builder.Configuration["VOICELIVE_MODE"];
```

- [ ] **Step 2: Apply the override where the bridge config is built**

In the `/ws/session` handler, replace:

```csharp
        var serverConfig = WebConfigLoader.LoadServerSession(configDir);
        await new VoiceLiveWebSocketBridge(serverConfig, logger).RunAsync(socket, context.RequestAborted);
```

with a single load plus the env override:

```csharp
        var loaded = WebConfigLoader.LoadServerSession(configDir);
        var serverConfig = loaded with { Mode = SessionModeResolver.Resolve(loaded.Mode, envSessionMode) };
        await new VoiceLiveWebSocketBridge(serverConfig, logger).RunAsync(socket, context.RequestAborted);
```

`VoiceLive.Web.Config` is already imported. The existing `catch (WebConfigValidationException ex)` around this block already handles an invalid `VOICELIVE_MODE` (which `Resolve` throws) by sending a startup error — no new catch needed.

- [ ] **Step 3: Build to verify it compiles**

Run: `dotnet build web/VoiceLive.Web.sln`
Expected: Build succeeded.

- [ ] **Step 4: Run the full web test suite (no regressions)**

Run: `dotnet test web/VoiceLive.Web.sln`
Expected: PASS (all existing + new tests).

- [ ] **Step 5: Commit**

```bash
git add web/src/VoiceLive.Web/Program.cs
git commit -m "feat(web): apply VOICELIVE_MODE env override to session mode

Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>
Copilot-Session: 3ec69cc9-32a0-41ae-84df-c1ff016b1860"
```

---

## Task 5: Web `SessionOptionsBuilder.BuildForAgent`

**Files:**
- Modify: `web/src/VoiceLive.Web/Session/SessionOptionsBuilder.cs`
- Test: `web/tests/VoiceLive.Web.Tests/ServerSessionConfigTests.cs`

- [ ] **Step 1: Write the failing test (append to `ServerSessionConfigTests`)**

```csharp
    [Fact]
    public void BuildForAgent_omits_model_and_instructions_but_keeps_voice_avatar_and_audio()
    {
        var config = WebConfigLoader.LoadServerSession(RepoConfigDir);

        var options = SessionOptionsBuilder.BuildForAgent(config);

        Assert.Null(options.Model);
        Assert.Null(options.Instructions);
        Assert.IsType<AzureStandardVoice>(options.Voice);
        Assert.Equal(InputAudioFormat.Pcm16, options.InputAudioFormat);
        Assert.Equal(OutputAudioFormat.Pcm16, options.OutputAudioFormat);
        Assert.Equal(24000, options.InputAudioSamplingRate);
        Assert.NotNull(options.Avatar);
        Assert.Equal("lisa", options.Avatar.Character);
        Assert.Equal("casual-sitting", options.Avatar.Style);
        Assert.Contains(options.Modalities, m => m.Equals(InteractionModality.Text));
        Assert.Contains(options.Modalities, m => m.Equals(InteractionModality.Audio));
    }
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test web/VoiceLive.Web.sln --filter FullyQualifiedName~BuildForAgent_omits_model_and_instructions`
Expected: FAIL — `SessionOptionsBuilder.BuildForAgent` does not exist (compile error).

- [ ] **Step 3: Refactor `Build` to share a common body and add `BuildForAgent`**

In `SessionOptionsBuilder.cs`, replace the existing `Build` method with a shared-core version plus `BuildForAgent`:

```csharp
    public static VoiceLiveSessionOptions Build(ServerSessionConfig config, string instructions)
    {
        var options = BuildCommon(config);
        options.Model = config.Model;
        options.Instructions = instructions;
        return options;
    }

    public static VoiceLiveSessionOptions BuildForAgent(ServerSessionConfig config)
        => BuildCommon(config); // agent owns Model + Instructions

    private static VoiceLiveSessionOptions BuildCommon(ServerSessionConfig config)
    {
        var options = new VoiceLiveSessionOptions
        {
            Voice = BuildVoice(config.Voice),
            TurnDetection = BuildTurnDetection(config.TurnTaking),
            InputAudioFormat = InputAudioFormat.Pcm16,
            OutputAudioFormat = OutputAudioFormat.Pcm16,
            InputAudioSamplingRate = config.InputAudioSamplingRate,
            Avatar = BuildAvatar(config.Avatar),
        };

        if (config.InputAudioNoiseReduction is not null)
            options.InputAudioNoiseReduction = new AudioNoiseReduction(new AudioNoiseReductionType(config.InputAudioNoiseReduction.Type));
        if (config.InputAudioEchoCancellation is not null && UsesTurnDetection(config.TurnTaking))
            options.InputAudioEchoCancellation = new AudioEchoCancellation();
        if (config.InputAudioTranscription is not null && UsesTurnDetection(config.TurnTaking))
        {
            options.InputAudioTranscription = new AudioInputTranscriptionOptions(new AudioInputTranscriptionOptionsModel(config.InputAudioTranscription.Model))
            {
                Language = config.InputAudioTranscription.Language
            };
        }

        options.Modalities.Clear();
        options.Modalities.Add(InteractionModality.Text);
        options.Modalities.Add(InteractionModality.Audio);
        return options;
    }
```

Leave `BuildVoice`, `BuildTurnDetection`, `UsesTurnDetection`, `BuildAvatar`, and all VAD/EOU helpers unchanged.

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test web/VoiceLive.Web.sln --filter FullyQualifiedName~ServerSessionConfigTests`
Expected: PASS (including the existing `Build_maps_gated_avatar_session_to_verified_sdk_options`, proving the refactor didn't change model-mode output).

- [ ] **Step 5: Commit**

```bash
git add web/src/VoiceLive.Web/Session/SessionOptionsBuilder.cs web/tests/VoiceLive.Web.Tests/ServerSessionConfigTests.cs
git commit -m "feat(web): add BuildForAgent session options (omit model+instructions)

Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>
Copilot-Session: 3ec69cc9-32a0-41ae-84df-c1ff016b1860"
```

---

## Task 6: Branch the bridge on mode + expose mode in `ready`

**Files:**
- Modify: `web/src/VoiceLive.Web/Session/VoiceLiveWebSocketBridge.cs`

This is integration code (talks to Azure); it is covered by the live E2E in Task 10, not a unit test.

- [ ] **Step 1: Branch session start + configuration on `config.Mode`**

In `RunAsync`, replace:

```csharp
            session = await client.StartSessionAsync(config.Model, cts.Token);
            var sessionOptions = SessionOptionsBuilder.Build(config, "You are a helpful assistant. Reply in concise, spoken sentences.");
            await session.ConfigureSessionAsync(sessionOptions, cts.Token);
```

with:

```csharp
            if (config.Mode == "agent")
            {
                logger.LogInformation("Starting Voice Live session in AGENT mode ({Agent} / {Project})", config.Agent.AgentName, config.Agent.AgentProjectName);
                var agent = new AgentSessionConfig(config.Agent.AgentName, config.Agent.AgentProjectName);
                session = await client.StartSessionAsync(SessionTarget.FromAgent(agent), cts.Token);
                await session.ConfigureSessionAsync(SessionOptionsBuilder.BuildForAgent(config), cts.Token);
            }
            else
            {
                logger.LogInformation("Starting Voice Live session in MODEL mode ({Model})", config.Model);
                session = await client.StartSessionAsync(config.Model, cts.Token);
                var sessionOptions = SessionOptionsBuilder.Build(config, "You are a helpful assistant. Reply in concise, spoken sentences.");
                await session.ConfigureSessionAsync(sessionOptions, cts.Token);
            }
```

- [ ] **Step 2: Add `mode` to the `ready` config block**

In the `SessionUpdateSessionUpdated` case, add `mode = config.Mode` to the `config = new { ... }` object:

```csharp
                            config = new
                            {
                                mode = config.Mode,
                                activeMode = config.TurnTaking.ActiveMode,
                                agentName = config.Agent.AgentName,
                                safeQuestions = config.Agent.SafeQuestions,
                                avatarCharacter = config.Avatar.Character,
                                avatarStyle = config.Avatar.Style
                            },
```

- [ ] **Step 3: Build to verify it compiles**

Run: `dotnet build web/VoiceLive.Web.sln`
Expected: Build succeeded. (`AgentSessionConfig`, `SessionTarget`, `SessionOptionsBuilder.BuildForAgent` all resolve — `Azure.AI.VoiceLive` is already imported.)

- [ ] **Step 4: Commit**

```bash
git add web/src/VoiceLive.Web/Session/VoiceLiveWebSocketBridge.cs
git commit -m "feat(web): connect via agent when mode=agent; expose mode in ready

Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>
Copilot-Session: 3ec69cc9-32a0-41ae-84df-c1ff016b1860"
```

---

## Task 7: Tool-event observability (server side)

**Files:**
- Create: `web/src/VoiceLive.Web/Session/ToolNotification.cs`
- Test: `web/tests/VoiceLive.Web.Tests/ToolNotificationTests.cs`
- Modify: `web/src/VoiceLive.Web/Session/VoiceLiveWebSocketBridge.cs`

- [ ] **Step 1: Write the failing test for the wire contract**

```csharp
using System.Text.Json;
using VoiceLive.Web.Session;
using Xunit;

public class ToolNotificationTests
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    [Fact]
    public void Serializes_to_stable_tool_frame()
    {
        var json = JsonSerializer.Serialize(new ToolNotification("done", "get_weather", "call_1"), Web);

        Assert.Contains("\"t\":\"tool\"", json);
        Assert.Contains("\"phase\":\"done\"", json);
        Assert.Contains("\"name\":\"get_weather\"", json);
        Assert.Contains("\"callId\":\"call_1\"", json);
    }

    [Fact]
    public void Allows_null_name_and_callId()
    {
        var json = JsonSerializer.Serialize(new ToolNotification("list", null, "item_9"), Web);
        Assert.Contains("\"phase\":\"list\"", json);
        Assert.Contains("\"name\":null", json);
        Assert.Contains("\"callId\":\"item_9\"", json);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test web/VoiceLive.Web.sln --filter FullyQualifiedName~ToolNotificationTests`
Expected: FAIL — `ToolNotification` does not exist (compile error).

- [ ] **Step 3: Implement `ToolNotification`**

```csharp
namespace VoiceLive.Web.Session;

/// <summary>Stable browser wire frame announcing an agent tool/function/MCP event (diagnostic only).</summary>
public sealed record ToolNotification(string Phase, string? Name, string? CallId)
{
    public string T => "tool";
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test web/VoiceLive.Web.sln --filter FullyQualifiedName~ToolNotificationTests`
Expected: PASS (2 tests).

- [ ] **Step 5: Add tool-event cases + `SendToolAsync` to the bridge**

In `VoiceLiveWebSocketBridge.cs`, add these cases to the `switch (update)` in `PumpVoiceLiveUpdatesAsync` (place them just before `case SessionUpdateError error:`). Property names are verified against `Azure.AI.VoiceLive` 1.1.0:

```csharp
                case SessionUpdateResponseFunctionCallArgumentsDelta fnDelta:
                    await SendToolAsync(socket, "args", name: null, fnDelta.CallId, ct);
                    break;
                case SessionUpdateResponseFunctionCallArgumentsDone fnDone:
                    logger.LogInformation("Agent tool call: {Name} (callId {CallId})", fnDone.Name, fnDone.CallId);
                    await SendToolAsync(socket, "done", fnDone.Name, fnDone.CallId, ct);
                    break;
                case SessionUpdateMcpListToolsInProgress mcpStart:
                    await SendToolAsync(socket, "list", name: null, mcpStart.ItemId, ct);
                    break;
                case SessionUpdateMcpListToolsCompleted mcpDone:
                    logger.LogInformation("Agent MCP tools listed (itemId {ItemId})", mcpDone.ItemId);
                    await SendToolAsync(socket, "list-done", name: null, mcpDone.ItemId, ct);
                    break;
                case SessionUpdateMcpListToolsFailed mcpFail:
                    logger.LogWarning("Agent MCP tool listing failed (itemId {ItemId})", mcpFail.ItemId);
                    await SendToolAsync(socket, "list-failed", name: null, mcpFail.ItemId, ct);
                    break;
```

Add the helper method near the other `Send*` helpers in the same class:

```csharp
    private Task SendToolAsync(WebSocket socket, string phase, string? name, string? callId, CancellationToken ct)
        => SendJsonAsync(socket, new ToolNotification(phase, name, callId), ct);
```

- [ ] **Step 6: Build + run the full web suite**

Run: `dotnet test web/VoiceLive.Web.sln`
Expected: Build succeeded; PASS (all tests).

- [ ] **Step 7: Commit**

```bash
git add web/src/VoiceLive.Web/Session/ToolNotification.cs web/tests/VoiceLive.Web.Tests/ToolNotificationTests.cs web/src/VoiceLive.Web/Session/VoiceLiveWebSocketBridge.cs
git commit -m "feat(web): forward agent tool/function/MCP events to the browser

Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>
Copilot-Session: 3ec69cc9-32a0-41ae-84df-c1ff016b1860"
```

---

## Task 8: Frontend — show mode + tool events

**Files:**
- Modify: `web/frontend/src/views.ts`
- Modify: `web/frontend/src/main.ts`
- Modify (build artifact): `web/src/VoiceLive.Web/wwwroot/app.js`

The frontend has no unit tests in this repo (build + E2E only). Implement, bundle, then verify in Task 10.

- [ ] **Step 1: Extend `ReadyConfig` + `OperatorView` and add a tools panel in `views.ts`**

Add `mode` to `ReadyConfig`:

```typescript
export type ReadyConfig = {
  mode?: string;
  activeMode: string;
  agentName: string;
  safeQuestions: string[];
  avatarCharacter?: string;
  avatarStyle?: string;
};
```

Add `noteTool` to the `OperatorView` type (after `addTranscript`):

```typescript
  addTranscript(role: "user" | "agent", text: string, final: boolean): void;
  noteTool(text: string): void;
```

In `renderOperatorView`, add a session-mode line to the config panel. Replace:

```typescript
  const agentLine = document.createElement("p");
  agentLine.textContent = "Agent: waiting for server";
  const modeLine = document.createElement("p");
  modeLine.textContent = "Turn-taking: waiting for server";
  const avatarLine = document.createElement("p");
  avatarLine.textContent = "Avatar: waiting for server";
  configPanel.append(agentLine, modeLine, avatarLine);
```

with:

```typescript
  const agentLine = document.createElement("p");
  agentLine.textContent = "Agent: waiting for server";
  const sessionModeLine = document.createElement("p");
  sessionModeLine.textContent = "Session mode: waiting for server";
  const modeLine = document.createElement("p");
  modeLine.textContent = "Turn-taking: waiting for server";
  const avatarLine = document.createElement("p");
  avatarLine.textContent = "Avatar: waiting for server";
  configPanel.append(agentLine, sessionModeLine, modeLine, avatarLine);
```

Add a tools panel before `shell.append(...)`:

```typescript
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
```

Update the `shell.append(...)` line to include `toolsPanel`:

```typescript
  shell.append(heading, error, avatarPanel, configPanel, statusPanel, controls, transcriptPanel, toolsPanel);
```

In the returned object, set the mode line inside `setConfig` (add after the `agentLine` set):

```typescript
    setConfig(config) {
      setText(agentLine, `Agent: ${config.agentName}`);
      setText(sessionModeLine, `Session mode: ${config.mode ?? "model"}`);
      setText(modeLine, `Turn-taking: ${config.activeMode}`);
```

Add the `noteTool` implementation to the returned object (after `addTranscript,`):

```typescript
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
```

- [ ] **Step 2: Handle the `tool` frame in `main.ts`**

Add the frame type to the `ServerFrame` union:

```typescript
type ToolFrame = { t: "tool"; phase: string; name?: string | null; callId?: string | null };
type ServerFrame =
  | ReadyFrame
  | AvatarAnswerFrame
  | TranscriptFrame
  | ErrorFrame
  | ToolFrame
  | { t: "speech-started" | "speech-stopped" | "avatar-speaking" | "avatar-idle" | "response-done" };
```

Add a `case "tool":` to the `switch (frame.t)` in `onMessage` (before `case "error":`):

```typescript
      case "tool": {
        const label = frame.name ? `${frame.phase}: ${frame.name}` : frame.phase;
        const idSuffix = frame.callId ? ` (${frame.callId})` : "";
        this.operator?.noteTool(`tool ${label}${idSuffix}`);
        break;
      }
```

- [ ] **Step 3: Build the bundle**

Run: `cd web/frontend && npm run build`
Expected: `../src/VoiceLive.Web/wwwroot/app.js` emitted with no TypeScript errors.

- [ ] **Step 4: Commit**

```bash
git add web/frontend/src/views.ts web/frontend/src/main.ts web/src/VoiceLive.Web/wwwroot/app.js
git commit -m "feat(web): show session mode + tool activity in operator UI

Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>
Copilot-Session: 3ec69cc9-32a0-41ae-84df-c1ff016b1860"
```

---

## Task 9: Documentation

**Files:**
- Modify: `docs/config-schema.md`
- Modify: `web/README.md`

- [ ] **Step 1: Document `mode` in `docs/config-schema.md`**

Under the `session.json` section, add a row/paragraph describing the field. Use wording consistent with the file's existing style:

```markdown
- `mode` *(optional, default `"model"`)* — how the session is established:
  - `"model"` — connect directly to the realtime `model` (default; the agent is ignored).
  - `"agent"` — connect to the Foundry agent from `agent.json` via `SessionTarget.FromAgent`; the
    agent owns model + instructions + tools, so `session.json.model` and instructions are ignored.
    Voice, avatar, audio, and turn-taking still apply.
  - The environment variable `VOICELIVE_MODE` (`model`|`agent`) overrides this field when set.
  - Applies to the **web** app. The CLI selects mode with its `--mode` flag instead.
```

- [ ] **Step 2: Document agent mode in `web/README.md`**

Add a short subsection:

```markdown
## Agent mode

By default the web app runs in **model mode** (connects to `session.json`'s `model`). To have it
connect to the Foundry agent named in `config/agent.json` instead — so the agent's server-side
hosted tools run — set the session mode to `agent`, either in config or via env var:

```bash
# via config: set "mode": "agent" in config/session.json, or override at runtime:
ConfigDir=$PWD/config VOICELIVE_MODE=agent ASPNETCORE_URLS=http://127.0.0.1:5210 \
  dotnet run --no-launch-profile --project web/src/VoiceLive.Web
```

Tool/function/MCP events emitted by the agent are logged and shown under "Tool activity" in the
operator view. Note: purely hosted tools (e.g. web search, Azure AI Search) run entirely
server-side and may not emit a discrete client event.
```

- [ ] **Step 3: Commit**

```bash
git add docs/config-schema.md web/README.md
git commit -m "docs(web): document session mode and VOICELIVE_MODE override

Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>
Copilot-Session: 3ec69cc9-32a0-41ae-84df-c1ff016b1860"
```

---

## Task 10: Live end-to-end verification (agent mode + avatar + tool observability)

**Files:** none committed (uses the existing Playwright harness at `/tmp/e2e/run.mjs`). Requires `az login`.

- [ ] **Step 1: Full web suite + build green**

Run: `dotnet test web/VoiceLive.Web.sln && cd web/frontend && npm run build && cd ../..`
Expected: all tests PASS; bundle emits with no drift beyond Task 8's committed `app.js`.

- [ ] **Step 2: Start the web app in agent mode**

Run (async/background):
```bash
ConfigDir=$PWD/config VOICELIVE_MODE=agent ASPNETCORE_URLS=http://127.0.0.1:5210 \
  dotnet run --no-launch-profile --project web/src/VoiceLive.Web
```
Verify: `curl -s http://127.0.0.1:5210/api/health` → `{"status":"ok"}`.

- [ ] **Step 3: Drive the operator E2E and assert avatar + agent**

Run: `node /tmp/e2e/run.mjs` (the harness that opens `/?view=operator`, feeds fake mic media, sends a safe question, and instruments WS + RTC).
Expected:
- The `ready` frame's `config.mode === "agent"` and `config.agentName === "company-direction-avatar"`.
- `avatar-answer` carries raw SDP (`m=video`/`m=audio`); `ontrack` fires; RTC reaches `connected`.
- A safe question produces `agent-transcript` deltas then `response-done`, in the agent's voice/persona.

If any assertion fails, treat it as a bug: use `superpowers:systematic-debugging` (root-cause first), fix, re-run. Do not claim success without this passing.

- [ ] **Step 4: Confirm tool observability plumbing**

With the app still running, confirm the operator page shows the "Tool activity" panel and that the server logs the tool cases when a tool fires. Because `company-direction-avatar` currently has **no tools**, no tool event is expected yet — assert only that the panel exists and the plumbing compiles/renders (empty "No tool calls yet."). Record in the plan that end-to-end tool firing will be confirmable once a tool is added to the agent (out of scope here).

- [ ] **Step 5: Stop the app + record results**

Stop the background web process (by its PID). Note the E2E outcome (mode=agent, avatar OK, transcript OK) in this task. If agent-mode avatar behaves differently from model mode, store a repository memory with the verified detail.

- [ ] **Step 6: Final commit (if any uncommitted verification notes/artifacts)**

Only commit intended files. Ensure no temp dirs (`/tmp/...`, `.agent-cfg*`) are staged.

```bash
git status --short
```

---

## Self-Review notes (author)

- **Spec coverage:** §3.1 mode selection → Tasks 2–4, 8, 9; §3.2 bridge wiring → Tasks 5–6; §3.3 avatar spike → Task 1 + Task 10; §3.4 observability → Tasks 7–8; §4 config/docs → Tasks 3, 9; §5 testing → per-task tests + Task 10; §6 failure modes → resolver throw (Tasks 2–4), bridge error path reused (Task 6), avatar-reject gate (Task 1). All sections mapped.
- **Type consistency:** `SessionModeResolver.{Model,Agent,IsValid,Normalize,Resolve}`, `ServerSessionConfig.Mode`, `SessionOptionsBuilder.{Build,BuildForAgent,BuildCommon}`, `ToolNotification(Phase,Name,CallId)` + `T`, browser frame `{t:"tool",phase,name,callId}`, `OperatorView.noteTool`, `ReadyConfig.mode` — used consistently across tasks.
- **Verified SDK facts baked in:** function-call event props (`CallId`, `Delta`; `CallId`,`Name`,`Arguments`), MCP events expose only `ItemId`; `ConnectAvatarAsync`/`FromAgent` present; no Bing connection (web_search unavailable) — so no task assumes a hosted tool exists.
- **Gate:** Task 1 must PASS before Tasks 2–10 are meaningful for the live path; the code/unit-test tasks (2–9) are still valid to implement, but Task 10 (and the feature's purpose) depend on the spike.
