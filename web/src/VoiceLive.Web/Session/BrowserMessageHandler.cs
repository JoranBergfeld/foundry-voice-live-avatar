using System.Text;
using System.Text.Json;
using Azure.AI.VoiceLive;
using VoiceLive.Web.Config;

namespace VoiceLive.Web.Session;

internal sealed class BrowserMessageHandler(
    ServerSessionConfig config,
    WebSocketTransport transport,
    ILogger logger)
{
    private string? _currentTurnId;

    internal async Task HandleAsync(VoiceLiveSession session, string json, CancellationToken ct)
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
                    await transport.SendJsonAsync(new { t = "pong" }, ct);
                    break;
            }
        }
    }

    private static string EncodeAvatarOffer(string rawOffer)
    {
        var wrapped = JsonSerializer.Serialize(new { type = "offer", sdp = rawOffer });
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(wrapped));
    }

    private static string CreateTurnId() => "turn-" + Guid.NewGuid().ToString("N");

    internal static bool TryGetControlType(string json, out string? type)
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
}