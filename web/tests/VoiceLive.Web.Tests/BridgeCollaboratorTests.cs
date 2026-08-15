using System.Diagnostics.Metrics;
using System.Net.WebSockets;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Azure.AI.VoiceLive;
using System.ClientModel.Primitives;
using VoiceLive.Web.Config;
using VoiceLive.Web.Session;

public class BridgeCollaboratorTests
{
    [Fact]
    public void VoiceLiveSessionFactory_has_exactly_one_nonpublic_instance_CreateAsync()
    {
        var methods = typeof(VoiceLiveSessionFactory)
            .GetMethods(BindingFlags.NonPublic | BindingFlags.Instance)
            .Where(method => method.Name == "CreateAsync");

        Assert.Single(methods);
    }

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
        Assert.Equal("message too big", socket.CloseDescriptionSent);
    }

    [Fact]
    public async Task Agent_session_updated_sends_ready_with_hosted_avatar_configuration()
    {
        var update = CreateSessionUpdated();
        var config = AppConfigLoader.Load(
            TestAppFactory.RepoConfigDir,
            new VoiceLiveOptions { Endpoint = "https://x", Mode = "agent", ApiVersion = "2025-10-01" }).Server;
        var socket = TestWebSocket.TextFragments();
        using var transport = new WebSocketTransport(socket);
        using var meter = new Meter("VoiceLive.Web.Tests");
        var handler = new VoiceLiveUpdateHandler(
            config,
            transport,
            Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance,
            meter.CreateCounter<long>("errors"));

        Assert.True(await handler.HandleAsync(update, CancellationToken.None));

        var ready = Assert.Single(socket.SentTexts);
        using var document = JsonDocument.Parse(ready);
        Assert.Equal("ready", document.RootElement.GetProperty("t").GetString());
        Assert.Equal("hosted-character", document.RootElement.GetProperty("config").GetProperty("avatarCharacter").GetString());
        Assert.Equal("hosted-style", document.RootElement.GetProperty("config").GetProperty("avatarStyle").GetString());
        Assert.Equal("turn:relay.example.test", document.RootElement.GetProperty("iceServers")[0].GetProperty("urls")[0].GetString());
    }

    [Fact]
    public async Task Agent_session_updated_does_not_fall_back_to_local_avatar_style()
    {
        var update = CreateSessionUpdated(avatarStyle: null);
        var config = AppConfigLoader.Load(
            TestAppFactory.RepoConfigDir,
            new VoiceLiveOptions { Endpoint = "https://x", Mode = "agent", ApiVersion = "2025-10-01" }).Server;
        var socket = TestWebSocket.TextFragments();
        using var transport = new WebSocketTransport(socket);
        using var meter = new Meter("VoiceLive.Web.Tests");
        var handler = new VoiceLiveUpdateHandler(
            config,
            transport,
            Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance,
            meter.CreateCounter<long>("errors"));

        Assert.True(await handler.HandleAsync(update, CancellationToken.None));

        using var ready = JsonDocument.Parse(Assert.Single(socket.SentTexts));
        Assert.Equal(JsonValueKind.Null, ready.RootElement.GetProperty("config").GetProperty("avatarStyle").ValueKind);
    }

    [Fact]
    public async Task Agent_session_created_waits_for_required_session_update()
    {
        var config = AppConfigLoader.Load(
            TestAppFactory.RepoConfigDir,
            new VoiceLiveOptions { Endpoint = "https://x", Mode = "agent", ApiVersion = "2025-10-01" }).Server;
        var socket = TestWebSocket.TextFragments();
        using var transport = new WebSocketTransport(socket);
        using var meter = new Meter("VoiceLive.Web.Tests");
        var handler = new VoiceLiveUpdateHandler(
            config,
            transport,
            Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance,
            meter.CreateCounter<long>("errors"));

        Assert.True(await handler.HandleAsync(CreateSessionCreated(), CancellationToken.None));

        Assert.Empty(socket.SentTexts);
    }

    [Fact]
    public async Task Model_session_created_waits_for_configured_session_update()
    {
        var config = AppConfigLoader.Load(
            TestAppFactory.RepoConfigDir,
            new VoiceLiveOptions { Endpoint = "https://x", Mode = "model", ApiVersion = "2025-10-01" }).Server;
        var socket = TestWebSocket.TextFragments();
        using var transport = new WebSocketTransport(socket);
        using var meter = new Meter("VoiceLive.Web.Tests");
        var handler = new VoiceLiveUpdateHandler(
            config,
            transport,
            Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance,
            meter.CreateCounter<long>("errors"));

        Assert.True(await handler.HandleAsync(CreateSessionCreated(), CancellationToken.None));

        Assert.Empty(socket.SentTexts);
    }

    [Fact]
    public async Task Response_audio_transcript_delta_forwards_agent_transcript_frame()
    {
        var config = AppConfigLoader.Load(
            TestAppFactory.RepoConfigDir,
            new VoiceLiveOptions { Endpoint = "https://x", Mode = "model", ApiVersion = "2025-10-01" }).Server;
        var socket = TestWebSocket.TextFragments();
        using var transport = new WebSocketTransport(socket);
        using var meter = new Meter("VoiceLive.Web.Tests");
        var handler = new VoiceLiveUpdateHandler(
            config,
            transport,
            Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance,
            meter.CreateCounter<long>("errors"));

        Assert.True(await handler.HandleAsync(CreateResponseAudioTranscriptDelta("Hel"), CancellationToken.None));

        using var frame = JsonDocument.Parse(Assert.Single(socket.SentTexts));
        Assert.Equal("agent-transcript", frame.RootElement.GetProperty("t").GetString());
        Assert.Equal("Hel", frame.RootElement.GetProperty("text").GetString());
        Assert.False(frame.RootElement.GetProperty("final").GetBoolean());
    }

    [Fact]
    public async Task Response_audio_transcript_done_forwards_final_agent_transcript_frame()
    {
        var config = AppConfigLoader.Load(
            TestAppFactory.RepoConfigDir,
            new VoiceLiveOptions { Endpoint = "https://x", Mode = "model", ApiVersion = "2025-10-01" }).Server;
        var socket = TestWebSocket.TextFragments();
        using var transport = new WebSocketTransport(socket);
        using var meter = new Meter("VoiceLive.Web.Tests");
        var handler = new VoiceLiveUpdateHandler(
            config,
            transport,
            Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance,
            meter.CreateCounter<long>("errors"));

        Assert.True(await handler.HandleAsync(CreateResponseAudioTranscriptDone("Hello there"), CancellationToken.None));

        using var frame = JsonDocument.Parse(Assert.Single(socket.SentTexts));
        Assert.Equal("agent-transcript", frame.RootElement.GetProperty("t").GetString());
        Assert.Equal("Hello there", frame.RootElement.GetProperty("text").GetString());
        Assert.True(frame.RootElement.GetProperty("final").GetBoolean());
    }

    [Fact]
    public async Task Response_text_delta_does_not_forward_agent_transcript_frame()
    {
        var config = AppConfigLoader.Load(
            TestAppFactory.RepoConfigDir,
            new VoiceLiveOptions { Endpoint = "https://x", Mode = "model", ApiVersion = "2025-10-01" }).Server;
        var socket = TestWebSocket.TextFragments();
        using var transport = new WebSocketTransport(socket);
        using var meter = new Meter("VoiceLive.Web.Tests");
        var handler = new VoiceLiveUpdateHandler(
            config,
            transport,
            Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance,
            meter.CreateCounter<long>("errors"));

        Assert.True(await handler.HandleAsync(CreateResponseTextDelta("Typed-only"), CancellationToken.None));

        Assert.Empty(socket.SentTexts);
    }

    private static SessionUpdateSessionCreated CreateSessionCreated()
    {
        return ModelReaderWriter.Read<SessionUpdateSessionCreated>(BinaryData.FromString(SessionEventJson("session.created")))
            ?? throw new InvalidOperationException("Could not deserialize test session.created event.");
    }

    private static SessionUpdateSessionUpdated CreateSessionUpdated(string? avatarStyle = "hosted-style")
    {
        return ModelReaderWriter.Read<SessionUpdateSessionUpdated>(BinaryData.FromString(SessionEventJson("session.updated", avatarStyle)))
            ?? throw new InvalidOperationException("Could not deserialize test session.updated event.");
    }

    private static SessionUpdateResponseAudioTranscriptDelta CreateResponseAudioTranscriptDelta(string delta)
    {
        return ModelReaderWriter.Read<SessionUpdateResponseAudioTranscriptDelta>(
            BinaryData.FromString(ResponseEventJson("response.audio_transcript.delta", "delta", delta)))
            ?? throw new InvalidOperationException("Could not deserialize test response.audio_transcript.delta event.");
    }

    private static SessionUpdateResponseAudioTranscriptDone CreateResponseAudioTranscriptDone(string transcript)
    {
        return ModelReaderWriter.Read<SessionUpdateResponseAudioTranscriptDone>(
            BinaryData.FromString(ResponseEventJson("response.audio_transcript.done", "transcript", transcript)))
            ?? throw new InvalidOperationException("Could not deserialize test response.audio_transcript.done event.");
    }

    private static SessionUpdateResponseTextDelta CreateResponseTextDelta(string delta)
    {
        return ModelReaderWriter.Read<SessionUpdateResponseTextDelta>(
            BinaryData.FromString(ResponseEventJson("response.text.delta", "delta", delta)))
            ?? throw new InvalidOperationException("Could not deserialize test response.text.delta event.");
    }

    private static string SessionEventJson(string type, string? avatarStyle = "hosted-style") => $$"""
            {
              "type": "{{type}}",
              "event_id": "evt-1",
              "session": {
                "id": "session-1",
                "model": "hosted-model",
                "modalities": ["text", "audio"],
                "avatar": {
                  "character": "hosted-character",
                  "style": {{JsonSerializer.Serialize(avatarStyle)}},
                  "customized": false,
                  "ice_servers": [
                    {
                      "urls": ["turn:relay.example.test"],
                      "username": "relay-user",
                      "credential": "relay-secret"
                    }
                  ]
                }
              }
            }
            """;

    private static string ResponseEventJson(string type, string valueProperty, string value)
    {
        return JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["type"] = type,
            ["event_id"] = "evt-1",
            ["response_id"] = "response-1",
            ["item_id"] = "item-1",
            ["output_index"] = 0,
            ["content_index"] = 0,
            [valueProperty] = value,
        });
    }

    private sealed class TestWebSocket : WebSocket
    {
        private readonly Queue<Fragment> _fragments;
        private Fragment? _currentFragment;
        private int _currentOffset;
        private WebSocketState _state = WebSocketState.Open;

        private TestWebSocket(IEnumerable<Fragment> fragments)
        {
            _fragments = new Queue<Fragment>(fragments);
        }

        internal WebSocketCloseStatus? CloseStatusSent { get; private set; }
        internal string? CloseDescriptionSent { get; private set; }
        internal List<string> SentTexts { get; } = [];

        public override WebSocketCloseStatus? CloseStatus => null;
        public override string? CloseStatusDescription => null;
        public override WebSocketState State => _state;
        public override string? SubProtocol => null;

        internal static TestWebSocket TextFragments(params string[] fragments)
            => new(fragments.Select((fragment, index) => new Fragment(
                Encoding.UTF8.GetBytes(fragment),
                WebSocketMessageType.Text,
                index == fragments.Length - 1)));

        internal static TestWebSocket Binary(byte[] payload)
            => new([new Fragment(payload, WebSocketMessageType.Binary, true)]);

        public override void Abort() => _state = WebSocketState.Aborted;

        public override Task CloseAsync(
            WebSocketCloseStatus closeStatus,
            string? statusDescription,
            CancellationToken cancellationToken)
        {
            CloseStatusSent = closeStatus;
            CloseDescriptionSent = statusDescription;
            _state = WebSocketState.Closed;
            return Task.CompletedTask;
        }

        public override Task CloseOutputAsync(
            WebSocketCloseStatus closeStatus,
            string? statusDescription,
            CancellationToken cancellationToken)
            => CloseAsync(closeStatus, statusDescription, cancellationToken);

        public override void Dispose() => _state = WebSocketState.Closed;

        public override Task<WebSocketReceiveResult> ReceiveAsync(
            ArraySegment<byte> buffer,
            CancellationToken cancellationToken)
        {
            _currentFragment ??= _fragments.Dequeue();
            var count = Math.Min(buffer.Count, _currentFragment.Payload.Length - _currentOffset);
            _currentFragment.Payload.AsSpan(_currentOffset, count).CopyTo(buffer.AsSpan());
            _currentOffset += count;
            var fragmentComplete = _currentOffset == _currentFragment.Payload.Length;
            var result = new WebSocketReceiveResult(
                count,
                _currentFragment.MessageType,
                fragmentComplete && _currentFragment.EndOfMessage);

            if (fragmentComplete)
            {
                _currentFragment = null;
                _currentOffset = 0;
            }

            return Task.FromResult(result);
        }

        public override Task SendAsync(
            ArraySegment<byte> buffer,
            WebSocketMessageType messageType,
            bool endOfMessage,
            CancellationToken cancellationToken)
        {
            if (messageType == WebSocketMessageType.Text)
                SentTexts.Add(Encoding.UTF8.GetString(buffer));
            return Task.CompletedTask;
        }

        private sealed record Fragment(
            byte[] Payload,
            WebSocketMessageType MessageType,
            bool EndOfMessage);
    }
}