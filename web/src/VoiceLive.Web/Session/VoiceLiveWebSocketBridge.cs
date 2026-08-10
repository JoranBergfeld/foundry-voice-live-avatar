using System.Diagnostics.Metrics;
using System.Net.WebSockets;
using System.Text;
using Azure;
using Azure.AI.VoiceLive;
using Azure.Core;
using VoiceLive.Web.Config;

namespace VoiceLive.Web.Session;

public sealed class VoiceLiveWebSocketBridge(
    ServerSessionConfig config,
    TokenCredential credential,
    string modelInstructions,
    HostedAgentSessionOptionsProvider hostedAgentOptions,
    ILogger<VoiceLiveWebSocketBridge> logger)
{
    private static readonly Meter Meter = new("VoiceLive.Web");
    private static readonly UpDownCounter<long> ActiveSessions = Meter.CreateUpDownCounter<long>("voicelive.active_sessions");
    private static readonly Histogram<double> SessionDurationMs = Meter.CreateHistogram<double>("voicelive.session_duration_ms");
    private static readonly Counter<long> Errors = Meter.CreateCounter<long>("voicelive.errors");

    public async Task RunAsync(WebSocket socket, CancellationToken requestAborted)
    {
        var sessionId = Guid.NewGuid().ToString("N")[..8];
        using var scope = logger.BeginScope("session:{SessionId}", sessionId);
        using var transport = new WebSocketTransport(socket, logger);
        var sessionFactory = new VoiceLiveSessionFactory(config, credential, modelInstructions, hostedAgentOptions, logger);
        var updateHandler = new VoiceLiveUpdateHandler(config, transport, logger, Errors);
        var browserHandler = new BrowserMessageHandler(config, transport, logger);
        var sw = System.Diagnostics.Stopwatch.StartNew();
        ActiveSessions.Add(1);
        try
        {
            VoiceLiveSession? session = null;
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(requestAborted);

            try
            {
                session = await sessionFactory.CreateAsync(cts.Token);

                var updateTask = PumpVoiceLiveUpdatesAsync(session, updateHandler, cts.Token);
                var browserTask = PumpBrowserMessagesAsync(session, transport, browserHandler, cts.Token);

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
                await transport.SendErrorAndCloseAsync(SafeError(ex), CancellationToken.None);
                return;
            }
            finally
            {
                if (session is not null)
                    await session.DisposeAsync();
            }

            await transport.CloseIfOpenAsync(WebSocketCloseStatus.NormalClosure, "session closed", CancellationToken.None);
        }
        finally
        {
            ActiveSessions.Add(-1);
            SessionDurationMs.Record(sw.Elapsed.TotalMilliseconds);
        }
    }

    private static async Task PumpVoiceLiveUpdatesAsync(
        VoiceLiveSession session,
        VoiceLiveUpdateHandler handler,
        CancellationToken ct)
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

    private string SafeError(Exception ex) => ex switch
    {
        WebConfigValidationException cfg => cfg.Message,
        HostedAgentConfigurationException cfg => cfg.Message,
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
