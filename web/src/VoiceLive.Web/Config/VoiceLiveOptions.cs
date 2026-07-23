namespace VoiceLive.Web.Config;

public sealed class VoiceLiveOptions
{
    public const string SectionName = "VoiceLive";
    public string Endpoint { get; set; } = "";
    public string ApiVersion { get; set; } = "2025-10-01";
    public string Mode { get; set; } = "model";
    public string ConfigDir { get; set; } = "config";
    public string GroundingFile { get; set; } = "grounding/company-direction.md";
    public string[] AllowedOrigins { get; set; } = [];
    public int MaxConcurrentSessions { get; set; } = 2;
}
