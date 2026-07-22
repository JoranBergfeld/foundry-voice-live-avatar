using System.Text.Json;
using VoiceLive.Cli.Config;
using VoiceLive.Cli.Session;
using Xunit;

public class SessionPayloadBuilderTests
{
    private static AppConfig Cfg(string activeMode, TurnMode mode)
    {
        var session = new SessionConfig("wss://x","swedencentral","2026-04-10","gpt-realtime",
            new VoiceConfig("azure-realtime-native","andrew"), 24000,
            new NoiseReduction("azure_deep_noise_suppression"),
            new EchoCancellation("server_echo_cancellation"),
            new Transcription("azure-speech","en"));
        var turn = new TurnTakingConfig(activeMode, new() { [activeMode] = mode });
        var agent = new AgentConfig("a","p",null,"resume","pack", new[]{"q"});
        return new AppConfig(session, turn, agent);
    }

    private static JsonElement BuildJson(AppConfig cfg)
        => JsonSerializer.SerializeToElement(SessionPayloadBuilder.Build(cfg));

    [Fact]
    public void Gated_mode_has_no_turn_detection_and_uses_snake_case()
    {
        var json = BuildJson(Cfg("gated", new TurnMode(ManualTurn: true, InterruptResponse: false)));
        Assert.False(json.TryGetProperty("turn_detection", out _));
        Assert.Equal("azure_deep_noise_suppression", json.GetProperty("input_audio_noise_reduction").GetProperty("type").GetString());
        Assert.Equal("andrew", json.GetProperty("voice").GetProperty("name").GetString());
    }

    [Fact]
    public void Open_mic_maps_azure_semantic_vad_and_eou()
    {
        var mode = new TurnMode(TurnDetection: new TurnDetectionConfig(
            "azure_semantic_vad", Threshold: 0.5, SilenceDurationMs: 500, InterruptResponse: true,
            EndOfUtteranceDetection: new EouDetection("semantic_detection_v1","medium",1000)));
        var td = BuildJson(Cfg("open-mic", mode)).GetProperty("turn_detection");
        Assert.Equal("azure_semantic_vad", td.GetProperty("type").GetString());
        Assert.True(td.GetProperty("interrupt_response").GetBoolean());
        Assert.Equal("semantic_detection_v1", td.GetProperty("end_of_utterance_detection").GetProperty("model").GetString());
        Assert.Equal(1000, td.GetProperty("end_of_utterance_detection").GetProperty("timeout_ms").GetInt32());
    }
}
