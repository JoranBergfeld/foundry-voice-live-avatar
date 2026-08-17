using System.Net.WebSockets;
using System.Reflection;
using System.Text;
using VoiceLive.Web.Session;

public class BridgeCollaboratorTests
{
    [Fact]
    public void VoiceLiveSessionFactory_has_exactly_one_nonpublic_instance_CreateAsync_method()
    {
        var methods = typeof(VoiceLiveSessionFactory)
            .GetMethods(BindingFlags.Instance | BindingFlags.NonPublic)
            .Where(method => method.Name == "CreateAsync");

        Assert.Single(methods);
    }

    [Fact]
    public async Task ReceiveMessageAsync_assembles_fragmented_text_messages()
    {
        using var socket = new TestWebSocket(
            Frame.Text("{\"t\":", endOfMessage: false),
            Frame.Text("\"ping\"}", endOfMessage: true));
        using var transport = new WebSocketTransport(socket);

        var message = await transport.ReceiveMessageAsync(CancellationToken.None);

        Assert.NotNull(message);
        Assert.Equal(WebSocketMessageType.Text, message.Value.Type);
        Assert.Equal("{\"t\":\"ping\"}", Encoding.UTF8.GetString(message.Value.Payload));
    }

    [Fact]
    public async Task ReceiveMessageAsync_closes_oversized_binary_messages_and_returns_null()
    {
        var frames = Enumerable.Range(0, 17)
            .Select(index => Frame.Binary(new byte[64 * 1024], endOfMessage: index == 16))
            .ToArray();
        using var socket = new TestWebSocket(frames);
        using var transport = new WebSocketTransport(socket);

        var message = await transport.ReceiveMessageAsync(CancellationToken.None);

        Assert.Null(message);
        Assert.Equal(WebSocketCloseStatus.MessageTooBig, socket.CloseStatusSent);
        Assert.Equal("message too big", socket.CloseDescriptionSent);
    }

    private readonly record struct Frame(byte[] Payload, WebSocketMessageType Type, bool EndOfMessage)
    {
        public static Frame Text(string payload, bool endOfMessage) =>
            new(Encoding.UTF8.GetBytes(payload), WebSocketMessageType.Text, endOfMessage);

        public static Frame Binary(byte[] payload, bool endOfMessage) =>
            new(payload, WebSocketMessageType.Binary, endOfMessage);
    }

    private sealed class TestWebSocket(params Frame[] frames) : WebSocket
    {
        private readonly Queue<Frame> _frames = new(frames);
        private WebSocketState _state = WebSocketState.Open;

        public WebSocketCloseStatus? CloseStatusSent { get; private set; }
        public string? CloseDescriptionSent { get; private set; }
        public override WebSocketCloseStatus? CloseStatus => null;
        public override string? CloseStatusDescription => null;
        public override WebSocketState State => _state;
        public override string? SubProtocol => null;

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
            CancellationToken cancellationToken) =>
            CloseAsync(closeStatus, statusDescription, cancellationToken);

        public override void Dispose() => _state = WebSocketState.Closed;

        public override Task<WebSocketReceiveResult> ReceiveAsync(
            ArraySegment<byte> buffer,
            CancellationToken cancellationToken)
        {
            var frame = _frames.Dequeue();
            frame.Payload.AsSpan().CopyTo(buffer.AsSpan());
            return Task.FromResult(new WebSocketReceiveResult(
                frame.Payload.Length,
                frame.Type,
                frame.EndOfMessage));
        }

        public override Task SendAsync(
            ArraySegment<byte> buffer,
            WebSocketMessageType messageType,
            bool endOfMessage,
            CancellationToken cancellationToken) => Task.CompletedTask;
    }
}