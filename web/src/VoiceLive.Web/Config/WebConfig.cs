using System.Text.Json;

namespace VoiceLive.Web.Config;

public sealed record VoiceConfig(string Type, string Name);

public sealed record ClientConfig(
    string Region,
    string ApiVersion,
    string Model,
    VoiceConfig Voice,
    JsonElement Avatar,
    string ActiveMode,
    string AgentName,
    string AgentProjectName,
    IReadOnlyList<string> SafeQuestions);

public sealed class WebConfigValidationException(string message) : Exception(message);

public static partial class WebConfigLoader
{
    private static readonly string[] VoiceTypes = ["azure-realtime-native", "azure-standard", "azure-custom", "openai"];
}
