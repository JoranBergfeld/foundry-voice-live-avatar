using Azure.Core;
using VoiceLive.Web.Config;

namespace VoiceLive.Web.Session;

public interface IVoiceLiveBridgeFactory
{
    VoiceLiveWebSocketBridge Create(AppConfig appConfig);
}

public sealed class VoiceLiveBridgeFactory(TokenCredential credential, ILoggerFactory loggerFactory)
    : IVoiceLiveBridgeFactory
{
    public VoiceLiveWebSocketBridge Create(AppConfig appConfig)
        => new(appConfig.Server, credential, appConfig.ModelInstructions, loggerFactory.CreateLogger<VoiceLiveWebSocketBridge>());
}
