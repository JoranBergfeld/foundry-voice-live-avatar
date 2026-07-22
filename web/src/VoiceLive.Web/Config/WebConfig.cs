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
    private static readonly JsonSerializerOptions Opts = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    private static readonly string[] VoiceTypes = ["azure-realtime-native", "azure-standard", "azure-custom", "openai"];
    private static readonly string[] Modes = ["open-mic", "gated", "hybrid"];

    public static ClientConfig Load(string dir)
    {
        var errors = new List<string>();
        var session = Read<SessionFile>(dir, "session.json", errors);
        var turn = Read<TurnTakingFile>(dir, "turntaking.json", errors);
        var agent = Read<AgentFile>(dir, "agent.json", errors);
        var avatar = ReadAvatar(dir, errors);

        if (session is not null)
        {
            Require(session.Region, "session.json", "region", errors);
            Require(session.ApiVersion, "session.json", "apiVersion", errors);
            Require(session.Model, "session.json", "model", errors);
            if (session.Voice is null)
            {
                errors.Add("session.json: voice: is required");
            }
            else
            {
                Require(session.Voice.Type, "session.json", "voice.type", errors);
                Require(session.Voice.Name, "session.json", "voice.name", errors);
                if (!string.IsNullOrWhiteSpace(session.Voice.Type) && !VoiceTypes.Contains(session.Voice.Type))
                    errors.Add($"session.json: voice.type: '{session.Voice.Type}' is not one of {string.Join(", ", VoiceTypes)}");
            }
        }

        if (turn is not null)
        {
            Require(turn.ActiveMode, "turntaking.json", "activeMode", errors);
            if (!string.IsNullOrWhiteSpace(turn.ActiveMode) && !Modes.Contains(turn.ActiveMode))
                errors.Add($"turntaking.json: activeMode: '{turn.ActiveMode}' is not one of {string.Join(", ", Modes)}");
        }

        if (agent is not null)
        {
            Require(agent.AgentName, "agent.json", "agentName", errors);
            Require(agent.AgentProjectName, "agent.json", "agentProjectName", errors);
            if (agent.SafeQuestions is null) errors.Add("agent.json: safeQuestions: is required");
        }

        if (avatar is null) errors.Add("avatar.json: avatar: is required");

        if (errors.Count > 0)
            throw new WebConfigValidationException("Configuration is invalid:\n  - " + string.Join("\n  - ", errors));

        return new ClientConfig(
            session!.Region!,
            session.ApiVersion!,
            session.Model!,
            new VoiceConfig(session.Voice!.Type!, session.Voice.Name!),
            avatar!.Value,
            turn!.ActiveMode!,
            agent!.AgentName!,
            agent.AgentProjectName!,
            agent.SafeQuestions!);
    }

    private static T? Read<T>(string dir, string file, List<string> errors) where T : class
    {
        var path = Path.Combine(dir, file);
        if (!File.Exists(path))
        {
            errors.Add($"{file}: file: not found at {path}");
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<T>(File.ReadAllText(path), Opts)
                ?? throw new JsonException("null document");
        }
        catch (JsonException ex)
        {
            errors.Add($"{file}: json: invalid JSON - {ex.Message}");
            return null;
        }
    }

    private static JsonElement? ReadAvatar(string dir, List<string> errors)
    {
        var path = Path.Combine(dir, "avatar.json");
        if (!File.Exists(path))
        {
            errors.Add($"avatar.json: file: not found at {path}");
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(path), new JsonDocumentOptions
            {
                CommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true
            });
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                errors.Add("avatar.json: root: must be an object");
                return null;
            }
            return doc.RootElement.Clone();
        }
        catch (JsonException ex)
        {
            errors.Add($"avatar.json: json: invalid JSON - {ex.Message}");
            return null;
        }
    }

    private static void Require(string? value, string file, string field, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(value)) errors.Add($"{file}: {field}: is required");
    }

    private sealed record SessionFile(string? Region, string? ApiVersion, string? Model, VoiceFile? Voice);
    private sealed record VoiceFile(string? Type, string? Name);
    private sealed record TurnTakingFile(string? ActiveMode);
    private sealed record AgentFile(string? AgentName, string? AgentProjectName, IReadOnlyList<string>? SafeQuestions);
}
