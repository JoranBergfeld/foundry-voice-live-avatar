using Azure.Core;
using VoiceLive.Web.Config;

namespace VoiceLive.Web.Session;

public interface IVoiceLiveBridgeFactory
{
    VoiceLiveWebSocketBridge Create(ServerSessionConfig config);
}

public sealed class VoiceLiveBridgeFactory(TokenCredential credential, ILoggerFactory loggerFactory)
    : IVoiceLiveBridgeFactory
{
    public VoiceLiveWebSocketBridge Create(ServerSessionConfig config)
        => new(config, credential, loggerFactory.CreateLogger<VoiceLiveWebSocketBridge>());
}
