using System.Buffers;
using System.Diagnostics.Metrics;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Azure;
using Azure.AI.VoiceLive;
using Azure.Core;
using VoiceLive.Web.Config;

namespace VoiceLive.Web.Session;

public sealed class VoiceLiveWebSocketBridge(
    ServerSessionConfig config,
    TokenCredential credential,
    string modelInstructions,
    ILogger<VoiceLiveWebSocketBridge> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly Meter Meter = new("VoiceLive.Web");
    private static readonly UpDownCounter<long> ActiveSessions = Meter.CreateUpDownCounter<long>("voicelive.active_sessions");
    private static readonly Histogram<double> SessionDurationMs = Meter.CreateHistogram<double>("voicelive.session_duration_ms");
    private static readonly Counter<long> Errors = Meter.CreateCounter<long>("voicelive.errors");
    private readonly SemaphoreSlim _sendLock = new(1, 1);
    private IReadOnlyList<object> _iceServers = [];
    private string? _currentTurnId;
    private bool _readySent;
    private const int MaxMessageBytes = 1024 * 1024;

    public async Task RunAsync(WebSocket socket, CancellationToken requestAborted)
    {
        var sessionId = Guid.NewGuid().ToString("N")[..8];
        using var scope = logger.BeginScope("session:{SessionId}", sessionId);
        var sw = System.Diagnostics.Stopwatch.StartNew();
        ActiveSessions.Add(1);
        try
        {
            VoiceLiveSession? session = null;
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(requestAborted);

            try
            {
                var serviceVersion = VoiceLiveServiceVersionMapper.Map(config.ApiVersion);
                var client = new VoiceLiveClient(
                    new Uri(config.Endpoint),
                    credential,
                    new VoiceLiveClientOptions(serviceVersion));

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
                    var sessionOptions = SessionOptionsBuilder.Build(config, modelInstructions);
                    await session.ConfigureSessionAsync(sessionOptions, cts.Token);
                }

                var updateTask = PumpVoiceLiveUpdatesAsync(session, socket, cts.Token);
                var browserTask = PumpBrowserMessagesAsync(session, socket, cts.Token);

                await Task.WhenAny(updateTask, browserTask);
                cts.Cancel();
                await Task.WhenAll(SwallowCancellation(updateTask), SwallowCancellation(browserTask));
            }
            catch (OperationCanceledException) when (requestAborted.IsCancellationRequested || socket.State is WebSocketState.Closed or WebSocketState.Aborted)
            {
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Voice Live WebSocket bridge failed");
                await SendErrorAndCloseAsync(socket, SafeError(ex), CancellationToken.None);
                return;
            }
            finally
            {
                if (session is not null)
                    await session.DisposeAsync();
            }

            await CloseIfOpenAsync(socket, WebSocketCloseStatus.NormalClosure, "session closed", CancellationToken.None);
        }
        finally
        {
            ActiveSessions.Add(-1);
            SessionDurationMs.Record(sw.Elapsed.TotalMilliseconds);
        }
    }

    private async Task PumpVoiceLiveUpdatesAsync(VoiceLiveSession session, WebSocket socket, CancellationToken ct)
    {
        await foreach (var update in session.GetUpdatesAsync(ct))
        {
            switch (update)
            {
                case SessionUpdateSessionUpdated updated:
                    _iceServers = BuildIceServers(updated.Session?.Avatar?.IceServers);
                    if (!_readySent)
                    {
                        _readySent = true;
                        await SendJsonAsync(socket, new
                        {
                            t = "ready",
                            config = new
                            {
                                mode = config.Mode,
                                activeMode = config.TurnTaking.ActiveMode,
                                agentName = config.Agent.AgentName,
                                safeQuestions = config.Agent.SafeQuestions,
                                avatarCharacter = config.Avatar.Character,
                                avatarStyle = config.Avatar.Style
                            },
                            iceServers = _iceServers
                        }, ct);
                    }
                    break;
                case SessionUpdateConversationItemInputAudioTranscriptionDelta delta:
                    await SendJsonAsync(socket, new { t = "user-transcript", text = delta.Delta, final = false }, ct);
                    break;
                case SessionUpdateConversationItemInputAudioTranscriptionCompleted completed:
                    await SendJsonAsync(socket, new { t = "user-transcript", text = completed.Transcript, final = true }, ct);
                    break;
                case SessionUpdateInputAudioBufferSpeechStarted:
                    await SendJsonAsync(socket, new { t = "speech-started" }, ct);
                    break;
                case SessionUpdateInputAudioBufferSpeechStopped:
                    await SendJsonAsync(socket, new { t = "speech-stopped" }, ct);
                    break;
                case ServerEventSessionAvatarSwitchToSpeaking:
                    await SendJsonAsync(socket, new { t = "avatar-speaking" }, ct);
                    break;
                case ServerEventSessionAvatarSwitchToIdle:
                    await SendJsonAsync(socket, new { t = "avatar-idle" }, ct);
                    break;
                case SessionUpdateAvatarConnecting avatar:
                    var rawAnswer = DecodeAvatarAnswer(avatar.ServerSdp);
                    if (rawAnswer is null)
                    {
                        await SendErrorAndCloseAsync(socket, "Avatar answer from the service could not be decoded.", ct);
                        return;
                    }
                    await SendJsonAsync(socket, new { t = "avatar-answer", sdp = rawAnswer }, ct);
                    break;
                case SessionUpdateResponseAudioTranscriptDelta transcript:
                    await SendJsonAsync(socket, new { t = "agent-transcript", text = transcript.Delta, final = false }, ct);
                    break;
                case SessionUpdateResponseAudioTranscriptDone transcriptDone:
                    await SendJsonAsync(socket, new { t = "agent-transcript", text = transcriptDone.Transcript, final = true }, ct);
                    break;
                case SessionUpdateResponseTextDelta text:
                    await SendJsonAsync(socket, new { t = "agent-transcript", text = text.Delta, final = false }, ct);
                    break;
                case SessionUpdateResponseDone:
                    await SendJsonAsync(socket, new { t = "response-done" }, ct);
                    break;
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
                case SessionUpdateError error:
                    var message = error.Error is null
                        ? "Voice Live service reported an error."
                        : $"Voice Live service error ({error.Error.Code ?? error.Error.Type}): {error.Error.Message}";
                    Errors.Add(1, new KeyValuePair<string, object?>("code", error.Error?.Code ?? error.Error?.Type ?? "unknown"));
                    await SendErrorAndCloseAsync(socket, message, ct);
                    return;
            }
        }
    }

    private async Task PumpBrowserMessagesAsync(VoiceLiveSession session, WebSocket socket, CancellationToken ct)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(64 * 1024);
        try
        {
            while (!ct.IsCancellationRequested && socket.State == WebSocketState.Open)
            {
                using var message = new MemoryStream();
                WebSocketReceiveResult result;
                do
                {
                    result = await socket.ReceiveAsync(buffer, ct);
                    if (result.MessageType == WebSocketMessageType.Close)
                        return;
                    message.Write(buffer, 0, result.Count);
                    if (message.Length > MaxMessageBytes)
                    {
                        logger.LogWarning("Browser message exceeded {Max} bytes; closing socket.", MaxMessageBytes);
                        await CloseIfOpenAsync(socket, WebSocketCloseStatus.MessageTooBig, "message too big", ct);
                        return;
                    }
                } while (!result.EndOfMessage);

                var payload = message.ToArray();
                if (result.MessageType == WebSocketMessageType.Binary)
                {
                    if (payload.Length > 0)
                        await session.SendInputAudioAsync(payload, ct);
                    continue;
                }

                if (result.MessageType == WebSocketMessageType.Text)
                    await HandleControlMessageAsync(session, socket, Encoding.UTF8.GetString(payload), ct);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private async Task HandleControlMessageAsync(VoiceLiveSession session, WebSocket socket, string json, CancellationToken ct)
    {
        JsonDocument doc;
        try { doc = JsonDocument.Parse(json); }
        catch (JsonException ex) { logger.LogDebug(ex, "Ignoring malformed control frame."); return; }
        using (doc)
        {
            if (!doc.RootElement.TryGetProperty("t", out var tProp)) return;

            switch (tProp.GetString())
            {
                case "avatar-offer":
                    if (doc.RootElement.TryGetProperty("sdp", out var sdp))
                        await session.ConnectAvatarAsync(EncodeAvatarOffer(sdp.GetString() ?? string.Empty), ct);
                    break;
                case "start-turn":
                    _currentTurnId = CreateTurnId();
                    await session.StartAudioTurnAsync(_currentTurnId, ct);
                    break;
                case "end-turn":
                    var turnId = _currentTurnId ?? CreateTurnId();
                    await session.EndAudioTurnAsync(turnId, ct);
                    _currentTurnId = null;
                    await session.CommitInputAudioAsync(ct);
                    await session.StartResponseAsync(ct);
                    break;
                case "barge-in":
                    await session.CancelResponseAsync(ct);
                    break;
                case "say":
                    if (doc.RootElement.TryGetProperty("text", out var text))
                    {
                        var prompt = text.GetString();
                        if (!string.IsNullOrWhiteSpace(prompt))
                        {
                            if (config.Mode == "agent")
                            {
                                // Agent mode ignores per-response instructions, so inject the
                                // operator prompt as a user turn to make the hosted agent respond.
                                await session.AddItemAsync(new UserMessageItem(prompt), ct);
                                await session.StartResponseAsync(ct);
                            }
                            else
                            {
                                await session.StartResponseAsync(prompt, ct);
                            }
                        }
                    }
                    break;
                case "ping":
                    await SendJsonAsync(socket, new { t = "pong" }, ct);
                    break;
            }
        }
    }

    private async Task SendJsonAsync(WebSocket socket, object payload, CancellationToken ct)
    {
        if (socket.State != WebSocketState.Open) return;

        var bytes = JsonSerializer.SerializeToUtf8Bytes(payload, JsonOptions);
        await _sendLock.WaitAsync(ct);
        try
        {
            if (socket.State == WebSocketState.Open)
                await socket.SendAsync(bytes, WebSocketMessageType.Text, WebSocketMessageFlags.EndOfMessage, ct);
        }
        finally
        {
            _sendLock.Release();
        }
    }

    private Task SendToolAsync(WebSocket socket, string phase, string? name, string? callId, CancellationToken ct)
        => SendJsonAsync(socket, new ToolNotification(phase, name, callId), ct);

    private async Task SendErrorAndCloseAsync(WebSocket socket, string message, CancellationToken ct)
    {
        try
        {
            await SendJsonAsync(socket, new { t = "error", message }, ct);
        }
        catch (Exception ex) when (ex is WebSocketException or OperationCanceledException)
        {
        }

        await CloseIfOpenAsync(socket, WebSocketCloseStatus.InternalServerError, "Voice Live session failed", ct);
    }

    private async Task CloseIfOpenAsync(WebSocket socket, WebSocketCloseStatus status, string description, CancellationToken ct)
    {
        if (socket.State is not (WebSocketState.Open or WebSocketState.CloseReceived))
            return;

        try
        {
            await _sendLock.WaitAsync(ct);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        try
        {
            if (socket.State is WebSocketState.Open or WebSocketState.CloseReceived)
                await socket.CloseAsync(status, description, ct);
        }
        catch (Exception ex) when (ex is WebSocketException or OperationCanceledException or InvalidOperationException)
        {
        }
        finally
        {
            _sendLock.Release();
        }
    }

    private static IReadOnlyList<object> BuildIceServers(IList<IceServer>? iceServers) => iceServers is null
        ? []
        : iceServers.Select(s => new { urls = s.Uris.ToArray(), username = s.Username, credential = s.Credential }).Cast<object>().ToArray();

    private static string EncodeAvatarOffer(string rawOffer)
    {
        var wrapped = JsonSerializer.Serialize(new { type = "offer", sdp = rawOffer });
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(wrapped));
    }

    private static string? DecodeAvatarAnswer(string? serverSdp)
    {
        if (string.IsNullOrEmpty(serverSdp)) return null;
        try
        {
            var json = Encoding.UTF8.GetString(Convert.FromBase64String(serverSdp));
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.TryGetProperty("sdp", out var s) ? s.GetString() : null;
        }
        catch (Exception ex) when (ex is FormatException or JsonException)
        {
            return null;
        }
    }

    private static string CreateTurnId() => "turn-" + Guid.NewGuid().ToString("N");

    public static bool TryGetControlType(string json, out string? type)
    {
        type = null;
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("t", out var t)) type = t.GetString();
            return true;
        }
        catch (JsonException) { return false; }
    }

    private string SafeError(Exception ex) => ex switch
    {
        WebConfigValidationException cfg => cfg.Message,
        RequestFailedException rfe => $"Voice Live request failed (status {rfe.Status}, code {rfe.ErrorCode}). Check endpoint, model, API version, and Azure role assignments.",
        _ => "Voice Live session failed. Check server logs and Azure credential/configuration."
    };

    private static async Task SwallowCancellation(Task task)
    {
        try
        {
            await task;
        }
        catch (OperationCanceledException)
        {
        }
        catch (WebSocketException)
        {
        }
    }
}
