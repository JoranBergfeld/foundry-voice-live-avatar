using Azure.AI.VoiceLive;

namespace VoiceLive.Web.Session;

public static class VoiceLiveServiceVersionMapper
{
    public static VoiceLiveClientOptions.ServiceVersion Map(string? apiVersion, Action<string>? warn = null)
    {
        if (apiVersion == "2025-10-01") return VoiceLiveClientOptions.ServiceVersion.V2025_10_01;

        warn?.Invoke($"Unknown Voice Live API version '{apiVersion}', defaulting to 2025-10-01.");
        return VoiceLiveClientOptions.ServiceVersion.V2025_10_01;
    }
}
