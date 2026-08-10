using System.Net.Http.Headers;
using System.Text.Json;
using Azure;
using Azure.AI.VoiceLive;
using Azure.Core;
using VoiceLive.Web.Config;

namespace VoiceLive.Web.Session;

public sealed class HostedAgentSessionOptionsProvider(HttpClient httpClient, TokenCredential credential)
{
    private const string ConfigurationKey = "microsoft.voice-live.configuration";
    private static readonly TokenRequestContext FoundryScope = new(["https://ai.azure.com/.default"]);

    internal async Task<VoiceLiveSessionOptions> LoadAsync(
        Uri endpoint,
        ServerAgentConfig agent,
        CancellationToken ct)
    {
        var token = await credential.GetTokenAsync(FoundryScope, ct);
        var requestUri = new Uri(
            endpoint,
            $"/api/projects/{Uri.EscapeDataString(agent.AgentProjectName)}/agents/{Uri.EscapeDataString(agent.AgentName)}/versions?api-version=v1");
        using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);
        using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        if (!response.IsSuccessStatusCode)
            throw new HostedAgentConfigurationException(
                $"Could not read Voice Live configuration for hosted agent '{agent.AgentName}' (status {(int)response.StatusCode}).");

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
        var metadata = FindLatestMetadata(document.RootElement);
        var configuration = ReadConfiguration(metadata, agent.AgentName);
        return BuildOptions(configuration, agent.AgentName);
    }

    private static JsonElement FindLatestMetadata(JsonElement root)
    {
        if (!root.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array)
            throw new HostedAgentConfigurationException("Hosted agent versions response did not contain a data array.");

        JsonElement? latest = null;
        var latestVersion = int.MinValue;
        foreach (var item in data.EnumerateArray())
        {
            if (!item.TryGetProperty("version", out var versionElement)
                || !int.TryParse(versionElement.GetString(), out var version)
                || version <= latestVersion)
                continue;

            latest = item;
            latestVersion = version;
        }

        if (latest is null || !latest.Value.TryGetProperty("metadata", out var metadata))
            throw new HostedAgentConfigurationException("Hosted agent has no published version metadata.");
        return metadata;
    }

    private static JsonDocument ReadConfiguration(JsonElement metadata, string agentName)
    {
        if (metadata.TryGetProperty(ConfigurationKey, out var direct))
        {
            var value = direct.GetString();
            if (!string.IsNullOrWhiteSpace(value))
            {
                var directDocument = JsonDocument.Parse(value);
                if (directDocument.RootElement.TryGetProperty("session", out _))
                    return directDocument;
                directDocument.Dispose();
            }
        }

        var chunks = metadata.EnumerateObject()
            .Where(property => property.Name.StartsWith(ConfigurationKey + ".chunk_", StringComparison.Ordinal))
            .Select(property => new
            {
                Index = int.TryParse(property.Name[(property.Name.LastIndexOf('_') + 1)..], out var index) ? index : int.MaxValue,
                Value = property.Value.GetString()
            })
            .Where(chunk => chunk.Value is not null)
            .OrderBy(chunk => chunk.Index)
            .Select(chunk => chunk.Value);
        var json = string.Concat(chunks);
        if (string.IsNullOrWhiteSpace(json))
            throw new HostedAgentConfigurationException(
                $"Hosted agent '{agentName}' does not contain Voice Live configuration metadata.");

        return JsonDocument.Parse(json);
    }

    private static VoiceLiveSessionOptions BuildOptions(JsonDocument configuration, string agentName)
    {
        using (configuration)
        {
            if (!configuration.RootElement.TryGetProperty("session", out var session))
                throw new HostedAgentConfigurationException(
                    $"Hosted agent '{agentName}' Voice Live configuration has no session object.");

            var options = new VoiceLiveSessionOptions
            {
                Voice = BuildVoice(session, agentName),
                Avatar = BuildAvatar(session, agentName)
            };

            if (session.TryGetProperty("turnDetection", out var turnDetection))
                options.TurnDetection = BuildTurnDetection(turnDetection);
            if (session.TryGetProperty("inputAudioTranscription", out var transcription))
                options.InputAudioTranscription = BuildTranscription(transcription);
            if (session.TryGetProperty("inputAudioNoiseReduction", out var noiseReduction)
                && noiseReduction.ValueKind == JsonValueKind.Object
                && noiseReduction.TryGetProperty("type", out var noiseType))
                options.InputAudioNoiseReduction = new AudioNoiseReduction(new AudioNoiseReductionType(noiseType.GetString()!));
            if (session.TryGetProperty("inputAudioEchoCancellation", out var echoCancellation)
                && echoCancellation.ValueKind == JsonValueKind.Object)
                options.InputAudioEchoCancellation = new AudioEchoCancellation();

            return options;
        }
    }

    private static VoiceProvider BuildVoice(JsonElement session, string agentName)
    {
        if (!session.TryGetProperty("voice", out var voice)
            || !voice.TryGetProperty("type", out var type)
            || !voice.TryGetProperty("name", out var name))
            throw new HostedAgentConfigurationException(
                $"Hosted agent '{agentName}' Voice Live configuration has no voice.");

        if (type.GetString() != "azure-standard")
            throw new HostedAgentConfigurationException(
                $"Hosted agent '{agentName}' uses unsupported voice type '{type.GetString()}'.");

        var result = new AzureStandardVoice(name.GetString()!);
        if (voice.TryGetProperty("temperature", out var temperature)) result.Temperature = temperature.GetSingle();
        if (voice.TryGetProperty("rate", out var rate)) result.Rate = rate.GetString();
        if (voice.TryGetProperty("style", out var style) && !string.IsNullOrWhiteSpace(style.GetString())) result.Style = style.GetString();
        return result;
    }

    private static AvatarConfiguration BuildAvatar(JsonElement session, string agentName)
    {
        if (!session.TryGetProperty("avatar", out var avatar)
            || !avatar.TryGetProperty("character", out var character)
            || string.IsNullOrWhiteSpace(character.GetString()))
            throw new HostedAgentConfigurationException(
                $"Hosted agent '{agentName}' Voice Live configuration has no avatar.");

        var customized = avatar.TryGetProperty("customized", out var customizedElement) && customizedElement.GetBoolean();
        var result = new AvatarConfiguration(character.GetString()!, customized);
        if (avatar.TryGetProperty("style", out var style) && !string.IsNullOrWhiteSpace(style.GetString()))
            result.Style = style.GetString();
        return result;
    }

    private static TurnDetection BuildTurnDetection(JsonElement turnDetection)
    {
        var type = turnDetection.GetProperty("type").GetString();
        TurnDetection result = type switch
        {
            "azure_semantic_vad" => new AzureSemanticVadTurnDetection(),
            "server_vad" => new ServerVadTurnDetection(),
            "none" => new NoTurnDetection(),
            _ => throw new HostedAgentConfigurationException($"Hosted agent uses unsupported turn-detection type '{type}'.")
        };
        if (turnDetection.TryGetProperty("removeFillerWords", out var removeFillerWords))
        {
            if (result is AzureSemanticVadTurnDetection semantic) semantic.RemoveFillerWords = removeFillerWords.GetBoolean();
        }
        return result;
    }

    private static AudioInputTranscriptionOptions BuildTranscription(JsonElement transcription)
    {
        var result = new AudioInputTranscriptionOptions(
            new AudioInputTranscriptionOptionsModel(transcription.GetProperty("model").GetString()!));
        if (transcription.TryGetProperty("language", out var language)
            && !string.Equals(language.GetString(), "auto-detect", StringComparison.OrdinalIgnoreCase))
            result.Language = language.GetString();
        return result;
    }
}

internal sealed class HostedAgentConfigurationException(string message) : Exception(message);