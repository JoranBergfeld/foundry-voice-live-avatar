using Azure.AI.VoiceLive;

namespace VoiceLive.Cli.Run;

public static class ServiceVersionMapper
{
    public static VoiceLiveClientOptions.ServiceVersion Map(string apiVersion, TextWriter? warnings = null)
    {
        return apiVersion.Trim().ToLowerInvariant() switch
        {
            "2025-10-01" => VoiceLiveClientOptions.ServiceVersion.V2025_10_01,
            "2026-01-01-preview" => VoiceLiveClientOptions.ServiceVersion.V2026_01_01_PREVIEW,
            _ => WarnAndDefault(apiVersion, warnings)
        };
    }

    private static VoiceLiveClientOptions.ServiceVersion WarnAndDefault(string apiVersion, TextWriter? warnings)
    {
        warnings?.WriteLine($"Warning: session.apiVersion '{apiVersion}' is not supported by Azure.AI.VoiceLive 1.1.0; using 2025-10-01.");
        return VoiceLiveClientOptions.ServiceVersion.V2025_10_01;
    }
}
