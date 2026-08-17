using System.Buffers;
using System.Net.WebSockets;
using System.Text.Json;

namespace VoiceLive.Web.Session;

internal readonly record struct BrowserSocketMessage(WebSocketMessageType Type, byte[] Payload);

internal sealed class WebSocketTransport(WebSocket socket) : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly SemaphoreSlim _sendLock = new(1, 1);

    internal const int MaxMessageBytes = 1024 * 1024;

    internal async Task<BrowserSocketMessage?> ReceiveMessageAsync(CancellationToken ct)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(64 * 1024);
        try
        {
            using var message = new MemoryStream();
            WebSocketReceiveResult result;
            do
            {
                result = await socket.ReceiveAsync(buffer, ct);
                if (result.MessageType == WebSocketMessageType.Close)
                    return null;
                message.Write(buffer, 0, result.Count);
                if (message.Length > MaxMessageBytes)
                {
                    await CloseIfOpenAsync(WebSocketCloseStatus.MessageTooBig, "message too big", ct);
                    return null;
                }
            } while (!result.EndOfMessage);

            return new BrowserSocketMessage(result.MessageType, message.ToArray());
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    internal async Task SendJsonAsync(object payload, CancellationToken ct)
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

    internal async Task SendErrorAndCloseAsync(string message, CancellationToken ct)
    {
        try
        {
            await SendJsonAsync(new { t = "error", message }, ct);
        }
        catch (Exception ex) when (ex is WebSocketException or OperationCanceledException)
        {
        }

        await CloseIfOpenAsync(WebSocketCloseStatus.InternalServerError, "Voice Live session failed", ct);
    }

    internal async Task CloseIfOpenAsync(
        WebSocketCloseStatus status,
        string description,
        CancellationToken ct)
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

    public void Dispose() => _sendLock.Dispose();
}