using System.Net.WebSockets;
using System.Reflection;
using System.Text;
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
            => Task.CompletedTask;

        private sealed record Fragment(
            byte[] Payload,
            WebSocketMessageType MessageType,
            bool EndOfMessage);
    }
}