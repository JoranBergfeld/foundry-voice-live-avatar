# Voice Live WebSocket Bridge Refactor Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Split `VoiceLiveWebSocketBridge` into focused collaborators without changing session behavior or the browser wire protocol.

**Architecture:** The bridge remains the session-lifetime orchestrator. Internal per-session collaborators own session creation, Voice Live update translation, browser control dispatch, and WebSocket transport; application DI and the bridge's public API remain unchanged.

**Tech Stack:** C# 14, .NET 10, ASP.NET Core WebSockets, Azure.AI.VoiceLive 1.1.0, xUnit

---

## File Structure

- Create `web/src/VoiceLive.Web/Session/VoiceLiveSessionFactory.cs` for SDK client/session creation and configuration.
- Create `web/src/VoiceLive.Web/Session/VoiceLiveUpdateHandler.cs` for service-update translation and service errors.
- Create `web/src/VoiceLive.Web/Session/BrowserMessageHandler.cs` for browser control dispatch and turn state.
- Create `web/src/VoiceLive.Web/Session/WebSocketTransport.cs` for receiving, sending, size limits, and closing.
- Modify `web/src/VoiceLive.Web/Session/VoiceLiveWebSocketBridge.cs` to retain orchestration, metrics, cancellation, and disposal.
- Modify `web/src/VoiceLive.Web/VoiceLive.Web.csproj` to expose internal collaborators to tests.
- Modify the existing control, SDP, and capacity tests to target the new owners.
- Create `web/tests/VoiceLive.Web.Tests/BridgeCollaboratorTests.cs` for transport and ownership tests.

### Task 1: Extract WebSocket Transport

**Files:**
- Modify: `web/src/VoiceLive.Web/VoiceLive.Web.csproj`
- Create: `web/src/VoiceLive.Web/Session/WebSocketTransport.cs`
- Create: `web/tests/VoiceLive.Web.Tests/BridgeCollaboratorTests.cs`

- [ ] **Step 1: Write failing transport tests**

Add test assembly visibility:

```xml
<ItemGroup>
  <InternalsVisibleTo Include="VoiceLive.Web.Tests" />
</ItemGroup>
```

Create a minimal test `WebSocket` that supplies configured fragments and records close calls, then add:

```csharp
[Fact]
public async Task ReceiveMessageAsync_assembles_fragments()
{
    var socket = TestWebSocket.TextFragments("{\"t\":", "\"ping\"}");
    using var transport = new WebSocketTransport(socket);

    var message = await transport.ReceiveMessageAsync(CancellationToken.None);

    Assert.NotNull(message);
    Assert.Equal(WebSocketMessageType.Text, message.Value.Type);
    Assert.Equal("{\"t\":\"ping\"}", Encoding.UTF8.GetString(message.Value.Payload));
}

[Fact]
public async Task ReceiveMessageAsync_closes_oversized_messages()
{
    var socket = TestWebSocket.Binary(new byte[WebSocketTransport.MaxMessageBytes + 1]);
    using var transport = new WebSocketTransport(socket);

    Assert.Null(await transport.ReceiveMessageAsync(CancellationToken.None));
    Assert.Equal(WebSocketCloseStatus.MessageTooBig, socket.CloseStatusSent);
}
```

- [ ] **Step 2: Run the tests and verify RED**

```bash
dotnet test web/tests/VoiceLive.Web.Tests/VoiceLive.Web.Tests.csproj --filter FullyQualifiedName~BridgeCollaboratorTests --no-restore
```

Expected: build failure because `WebSocketTransport` does not exist.

- [ ] **Step 3: Implement transport ownership**

Create these concrete APIs and move the existing implementations unchanged:

```csharp
internal readonly record struct BrowserSocketMessage(WebSocketMessageType Type, byte[] Payload);

internal sealed class WebSocketTransport(WebSocket socket) : IDisposable
{
    internal const int MaxMessageBytes = 1024 * 1024;
    internal Task<BrowserSocketMessage?> ReceiveMessageAsync(CancellationToken ct);
    internal Task SendJsonAsync(object payload, CancellationToken ct);
    internal Task SendErrorAndCloseAsync(string message, CancellationToken ct);
    internal Task CloseIfOpenAsync(WebSocketCloseStatus status, string description, CancellationToken ct);
}
```

`ReceiveMessageAsync` assembles fragments and returns `null` after peer close or oversized-message close. Preserve JSON web defaults, the send lock, socket-state checks, close statuses, and exception filters.

- [ ] **Step 4: Run the focused tests and verify GREEN**

Run the Task 1 command. Expected: all selected tests pass.

### Task 2: Extract Browser Control Handling

**Files:**
- Create: `web/src/VoiceLive.Web/Session/BrowserMessageHandler.cs`
- Modify: `web/tests/VoiceLive.Web.Tests/ControlMessageTests.cs`
- Modify: `web/tests/VoiceLive.Web.Tests/AvatarSdpCodecTests.cs`

- [ ] **Step 1: Retarget characterization tests**

Change parser tests to:

```csharp
var result = BrowserMessageHandler.TryGetControlType(json, out var type);
```

Change the offer-codec reflection target to `typeof(BrowserMessageHandler)`. Leave answer decoding targeted at the bridge until Task 3.

- [ ] **Step 2: Run the tests and verify RED**

```bash
dotnet test web/tests/VoiceLive.Web.Tests/VoiceLive.Web.Tests.csproj --filter "FullyQualifiedName~ControlMessageTests|FullyQualifiedName~AvatarSdpCodecTests" --no-restore
```

Expected: build failure because `BrowserMessageHandler` does not exist.

- [ ] **Step 3: Implement control dispatch**

Create:

```csharp
internal sealed class BrowserMessageHandler(
    ServerSessionConfig config,
    WebSocketTransport transport,
    ILogger logger)
{
    private string? _currentTurnId;

    internal Task HandleAsync(VoiceLiveSession session, string json, CancellationToken ct);
    internal static bool TryGetControlType(string json, out string? type);
    private static string EncodeAvatarOffer(string rawOffer);
    private static string CreateTurnId();
}
```

Move the existing `avatar-offer`, `start-turn`, `end-turn`, `barge-in`, `say`, and `ping` cases unchanged. Preserve malformed/unknown frame behavior and agent/model `say` differences.

- [ ] **Step 4: Run the focused tests and verify GREEN**

Run the Task 2 command. Expected: all selected tests pass.

### Task 3: Extract Update Translation and Session Creation

**Files:**
- Create: `web/src/VoiceLive.Web/Session/VoiceLiveUpdateHandler.cs`
- Create: `web/src/VoiceLive.Web/Session/VoiceLiveSessionFactory.cs`
- Modify: `web/tests/VoiceLive.Web.Tests/AvatarSdpCodecTests.cs`
- Modify: `web/tests/VoiceLive.Web.Tests/AvatarCapacityErrorTests.cs`
- Modify: `web/tests/VoiceLive.Web.Tests/BridgeCollaboratorTests.cs`

- [ ] **Step 1: Retarget characterization tests**

Change answer decoding and avatar-capacity reflection targets to `typeof(VoiceLiveUpdateHandler)`. Add this structural factory test:

```csharp
[Fact]
public void Session_factory_has_one_internal_creation_entry_point()
{
    var methods = typeof(VoiceLiveSessionFactory)
        .GetMethods(BindingFlags.Instance | BindingFlags.NonPublic)
        .Where(method => method.Name == "CreateAsync");
    Assert.Single(methods);
}
```

- [ ] **Step 2: Run the tests and verify RED**

```bash
dotnet test web/tests/VoiceLive.Web.Tests/VoiceLive.Web.Tests.csproj --filter "FullyQualifiedName~AvatarSdpCodecTests|FullyQualifiedName~AvatarCapacityErrorTests|FullyQualifiedName~BridgeCollaboratorTests" --no-restore
```

Expected: build failure because the update handler and session factory do not exist.

- [ ] **Step 3: Implement update translation**

Create:

```csharp
internal sealed class VoiceLiveUpdateHandler(
    ServerSessionConfig config,
    WebSocketTransport transport,
    ILogger logger,
    Counter<long> errors)
{
    private bool _readySent;
    internal Task<bool> HandleAsync(object update, CancellationToken ct);
    private static IReadOnlyList<object> BuildIceServers(IList<IceServer>? iceServers);
    private static string? DecodeAvatarAnswer(string? serverSdp);
    private static bool IsAvatarCapacityError(string? signal);
}
```

Move the complete service-update switch into `HandleAsync`. Return `false` only after an invalid avatar answer or fatal service error closes the socket. Preserve all browser payloads, tool phases, logs, metrics tags, and capacity-error text exactly.

- [ ] **Step 4: Implement session creation**

Create:

```csharp
internal sealed class VoiceLiveSessionFactory(
    ServerSessionConfig config,
    TokenCredential credential,
    string modelInstructions,
    ILogger logger)
{
    internal Task<VoiceLiveSession> CreateAsync(CancellationToken ct);
}
```

Move service-version mapping, SDK client construction, mode logging, target selection, and session configuration unchanged. Dispose a started session if configuration throws, then rethrow.

- [ ] **Step 5: Run focused tests and verify GREEN**

Run the Task 3 command plus `FullyQualifiedName~ServerSessionConfigTests`. Expected: all selected tests pass.

### Task 4: Reduce the Bridge to Orchestration

**Files:**
- Modify: `web/src/VoiceLive.Web/Session/VoiceLiveWebSocketBridge.cs`

- [ ] **Step 1: Wire the collaborators**

Construct one transport and both handlers per `RunAsync`, and create the SDK session through `VoiceLiveSessionFactory`. Retain logging scope, stopwatch, metrics, linked cancellation, concurrent pump coordination, exception mapping, and disposal.

Use these pump shapes:

```csharp
private static async Task PumpVoiceLiveUpdatesAsync(
    VoiceLiveSession session, VoiceLiveUpdateHandler handler, CancellationToken ct)
{
    await foreach (var update in session.GetUpdatesAsync(ct))
        if (!await handler.HandleAsync(update, ct)) return;
}

private static async Task PumpBrowserMessagesAsync(
    VoiceLiveSession session,
    WebSocketTransport transport,
    BrowserMessageHandler handler,
    CancellationToken ct)
{
    while (!ct.IsCancellationRequested)
    {
        var message = await transport.ReceiveMessageAsync(ct);
        if (message is null) return;
        if (message.Value.Type == WebSocketMessageType.Binary && message.Value.Payload.Length > 0)
            await session.SendInputAudioAsync(message.Value.Payload, ct);
        else if (message.Value.Type == WebSocketMessageType.Text)
            await handler.HandleAsync(session, Encoding.UTF8.GetString(message.Value.Payload), ct);
    }
}
```

- [ ] **Step 2: Remove migrated bridge members**

Remove transport state/helpers, session startup, the update switch, control dispatch, SDP codecs, control parser, turn ID state, and avatar-capacity classification. Keep `SafeError` and `SwallowCancellation` on the bridge.

- [ ] **Step 3: Run focused compatibility tests**

```bash
dotnet test web/tests/VoiceLive.Web.Tests/VoiceLive.Web.Tests.csproj --filter "FullyQualifiedName~BridgeCollaboratorTests|FullyQualifiedName~ControlMessageTests|FullyQualifiedName~AvatarSdpCodecTests|FullyQualifiedName~AvatarCapacityErrorTests|FullyQualifiedName~ToolNotificationTests|FullyQualifiedName~ServerSessionConfigTests" --no-restore
```

Expected: all selected tests pass.

### Task 5: Full Verification

**Files:**
- Review all files changed above.

- [ ] **Step 1: Run the complete backend suite**

```bash
dotnet test web/tests/VoiceLive.Web.Tests/VoiceLive.Web.Tests.csproj --no-restore
```

Expected: all tests pass without new warnings.

- [ ] **Step 2: Check the focused diff**

```bash
git --no-pager diff --check
git --no-pager diff --stat
git --no-pager diff -- web/src/VoiceLive.Web/Session web/src/VoiceLive.Web/VoiceLive.Web.csproj web/tests/VoiceLive.Web.Tests
```

Expected: no whitespace errors or protocol changes; the bridge contains orchestration rather than protocol details.

- [ ] **Step 3: Confirm frozen behavior**

Verify Agent/model startup, frame names and fields, the one-megabyte limit, close statuses, avatar-capacity behavior, metrics, and log messages against the pre-refactor implementation.