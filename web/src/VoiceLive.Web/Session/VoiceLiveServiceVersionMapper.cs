using Azure.AI.VoiceLive;

namespace VoiceLive.Web.Session;

public static class VoiceLiveServiceVersionMapper
{
    public static VoiceLiveClientOptions.ServiceVersion Map(string? apiVersion)
    {
        return apiVersion switch
        {
            "2025-10-01" => VoiceLiveClientOptions.ServiceVersion.V2025_10_01,
            _ => throw new VoiceLive.Web.Config.WebConfigValidationException(
                $"apiVersion '{apiVersion}' is not supported; supported: 2025-10-01.")
        };
    }
}
