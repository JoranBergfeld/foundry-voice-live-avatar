using System.Text.Json;

namespace VoiceLive.Cli.Config;

public sealed record AppConfig(SessionConfig Session, TurnTakingConfig TurnTaking, AgentConfig Agent);

public static class ConfigLoader
{
    private static readonly JsonSerializerOptions Opts = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    private static readonly string[] VoiceTypes = ["azure-realtime-native", "azure-standard", "azure-custom", "openai"];
    private static readonly string[] Modes = ["open-mic", "gated", "hybrid"];
    private static readonly string[] Grounding = ["pack", "rag", "both"];
    private static readonly string[] ResumePolicies = ["resume", "fresh"];

    public static AppConfig Load(string dir)
    {
        var errors = new List<string>();
        var session = Read<SessionConfig>(dir, "session.json", errors);
        var turn = Read<TurnTakingConfig>(dir, "turntaking.json", errors);
        var agent = Read<AgentConfig>(dir, "agent.json", errors);

        if (session is not null)
        {
            if (string.IsNullOrWhiteSpace(session.Endpoint)) errors.Add("session.json: endpoint is required");
            if (session.Voice is null || string.IsNullOrWhiteSpace(session.Voice.Type)) errors.Add("session.json: voice.type is required");
            else if (!VoiceTypes.Contains(session.Voice.Type)) errors.Add($"session.json: voice.type '{session.Voice.Type}' is not one of {string.Join(", ", VoiceTypes)}");
            if (session.InputAudioNoiseReduction is null || string.IsNullOrWhiteSpace(session.InputAudioNoiseReduction.Type)) errors.Add("session.json: inputAudioNoiseReduction.type is required");
            if (session.InputAudioEchoCancellation is null || string.IsNullOrWhiteSpace(session.InputAudioEchoCancellation.Type)) errors.Add("session.json: inputAudioEchoCancellation.type is required");
            if (session.InputAudioTranscription is null || string.IsNullOrWhiteSpace(session.InputAudioTranscription.Model)) errors.Add("session.json: inputAudioTranscription.model is required");
        }
        if (turn is not null)
        {
            if (!Modes.Contains(turn.ActiveMode)) errors.Add($"turntaking.json: activeMode '{turn.ActiveMode}' is not one of {string.Join(", ", Modes)}");
            else if (turn.Modes is null || !turn.Modes.ContainsKey(turn.ActiveMode)) errors.Add($"turntaking.json: activeMode '{turn.ActiveMode}' has no matching entry in modes");
        }
        if (agent is not null)
        {
            if (!Grounding.Contains(agent.GroundingStrategy)) errors.Add($"agent.json: groundingStrategy '{agent.GroundingStrategy}' is not one of {string.Join(", ", Grounding)}");
            if (!ResumePolicies.Contains(agent.ConversationResumePolicy)) errors.Add($"agent.json: conversationResumePolicy '{agent.ConversationResumePolicy}' is not one of {string.Join(", ", ResumePolicies)}");
        }

        if (errors.Count > 0)
            throw new ConfigValidationException("Configuration is invalid:\n  - " + string.Join("\n  - ", errors));

        return new AppConfig(session!, turn!, agent!);
    }

    private static T? Read<T>(string dir, string file, List<string> errors) where T : class
    {
        var path = Path.Combine(dir, file);
        if (!File.Exists(path)) { errors.Add($"{file}: file not found at {path}"); return null; }
        try { return JsonSerializer.Deserialize<T>(File.ReadAllText(path), Opts) ?? throw new JsonException("null document"); }
        catch (JsonException ex) { errors.Add($"{file}: invalid JSON - {ex.Message}"); return null; }
    }
}
