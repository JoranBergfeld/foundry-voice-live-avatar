using System.Diagnostics.Metrics;
using System.Text;
using System.Text.Json;
using Azure.AI.VoiceLive;
using VoiceLive.Web.Config;

namespace VoiceLive.Web.Session;

internal sealed class VoiceLiveUpdateHandler(
    ServerSessionConfig config,
    WebSocketTransport transport,
    ILogger logger,
    Counter<long> errors)
{
    private IReadOnlyList<object> _iceServers = [];
    private bool _readySent;

    internal async Task<bool> HandleAsync(object update, CancellationToken ct)
    {
        switch (update)
        {
            case SessionUpdateSessionUpdated updated:
                _iceServers = BuildIceServers(updated.Session?.Avatar?.IceServers);
                if (!_readySent)
                {
                    _readySent = true;
                    await transport.SendJsonAsync(new
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
                await transport.SendJsonAsync(new { t = "user-transcript", text = delta.Delta, final = false }, ct);
                break;
            case SessionUpdateConversationItemInputAudioTranscriptionCompleted completed:
                await transport.SendJsonAsync(new { t = "user-transcript", text = completed.Transcript, final = true }, ct);
                break;
            case SessionUpdateInputAudioBufferSpeechStarted:
                await transport.SendJsonAsync(new { t = "speech-started" }, ct);
                break;
            case SessionUpdateInputAudioBufferSpeechStopped:
                await transport.SendJsonAsync(new { t = "speech-stopped" }, ct);
                break;
            case ServerEventSessionAvatarSwitchToSpeaking:
                await transport.SendJsonAsync(new { t = "avatar-speaking" }, ct);
                break;
            case ServerEventSessionAvatarSwitchToIdle:
                await transport.SendJsonAsync(new { t = "avatar-idle" }, ct);
                break;
            case SessionUpdateAvatarConnecting avatar:
                var rawAnswer = DecodeAvatarAnswer(avatar.ServerSdp);
                if (rawAnswer is null)
                {
                    await transport.SendErrorAndCloseAsync("Avatar answer from the service could not be decoded.", ct);
                    return false;
                }
                await transport.SendJsonAsync(new { t = "avatar-answer", sdp = rawAnswer }, ct);
                break;
            case SessionUpdateResponseAudioTranscriptDelta transcript:
                await transport.SendJsonAsync(new { t = "agent-transcript", text = transcript.Delta, final = false }, ct);
                break;
            case SessionUpdateResponseAudioTranscriptDone transcriptDone:
                await transport.SendJsonAsync(new { t = "agent-transcript", text = transcriptDone.Transcript, final = true }, ct);
                break;
            case SessionUpdateResponseTextDelta text:
                await transport.SendJsonAsync(new { t = "agent-transcript", text = text.Delta, final = false }, ct);
                break;
            case SessionUpdateResponseDone:
                await transport.SendJsonAsync(new { t = "response-done" }, ct);
                break;
            case SessionUpdateResponseFunctionCallArgumentsDelta fnDelta:
                await SendToolAsync("args", name: null, fnDelta.CallId, ct);
                break;
            case SessionUpdateResponseFunctionCallArgumentsDone fnDone:
                logger.LogInformation("Agent tool call: {Name} (callId {CallId})", fnDone.Name, fnDone.CallId);
                await SendToolAsync("done", fnDone.Name, fnDone.CallId, ct);
                break;
            case SessionUpdateMcpListToolsInProgress mcpStart:
                await SendToolAsync("list", name: null, mcpStart.ItemId, ct);
                break;
            case SessionUpdateMcpListToolsCompleted mcpDone:
                logger.LogInformation("Agent MCP tools listed (itemId {ItemId})", mcpDone.ItemId);
                await SendToolAsync("list-done", name: null, mcpDone.ItemId, ct);
                break;
            case SessionUpdateMcpListToolsFailed mcpFail:
                logger.LogWarning("Agent MCP tool listing failed (itemId {ItemId})", mcpFail.ItemId);
                await SendToolAsync("list-failed", name: null, mcpFail.ItemId, ct);
                break;
            case SessionUpdateError error:
                var errorCode = error.Error?.Code ?? error.Error?.Type;
                errors.Add(1, new KeyValuePair<string, object?>("code", errorCode ?? "unknown"));
                if (IsAvatarCapacityError(errorCode))
                {
                    logger.LogWarning(
                        "Avatar rendering unavailable ({Code}); avatar video and audio are both lost. Service message: {Message}",
                        errorCode, error.Error?.Message);
                    await transport.SendJsonAsync(new
                    {
                        t = "avatar-error",
                        code = errorCode,
                        message = "Avatar rendering is unavailable on this Voice Live resource (capacity or quota). Avatar audio is lost along with the video, because both ride the same WebRTC peer connection — there is no voice-only fallback. Invoke your fallback plan. To fix: request an avatar rendering quota increase for this resource, or point VoiceLive__Endpoint at an avatar-enabled resource."
                    }, ct);
                    break;
                }
                var message = error.Error is null
                    ? "Voice Live service reported an error."
                    : $"Voice Live service error ({errorCode}): {error.Error.Message}";
                await transport.SendErrorAndCloseAsync(message, ct);
                return false;
        }

        return true;
    }

    private Task SendToolAsync(string phase, string? name, string? callId, CancellationToken ct)
        => transport.SendJsonAsync(new ToolNotification(phase, name, callId), ct);

    private static IReadOnlyList<object> BuildIceServers(IList<IceServer>? iceServers) => iceServers is null
        ? []
        : iceServers.Select(s => new { urls = s.Uris.ToArray(), username = s.Username, credential = s.Credential }).Cast<object>().ToArray();

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

    private static bool IsAvatarCapacityError(string? signal) =>
        !string.IsNullOrEmpty(signal)
        && signal.Contains("avatar", StringComparison.OrdinalIgnoreCase)
        && (signal.Contains("exhausted", StringComparison.OrdinalIgnoreCase)
            || signal.Contains("capacity", StringComparison.OrdinalIgnoreCase));
}