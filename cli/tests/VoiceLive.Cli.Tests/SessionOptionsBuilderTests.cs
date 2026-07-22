using Azure.AI.VoiceLive;
using VoiceLive.Cli.Config;
using VoiceLive.Cli.Session;
using Xunit;

public class SessionOptionsBuilderTests
{
    private static AppConfig Cfg(VoiceConfig? voice = null, string activeMode = "gated", TurnMode? mode = null, string? instructions = "test instructions")
    {
        var session = new SessionConfig(
            "https://example.services.ai.azure.com",
            "swedencentral",
            "2025-10-01",
            "gpt-realtime",
            voice ?? new VoiceConfig("azure-realtime-native", "en-US-AvaNeural"),
            24000,
            new NoiseReduction("azure_deep_noise_suppression"),
            new EchoCancellation("server_echo_cancellation"),
            new Transcription("azure-speech", "en"));
        var turn = new TurnTakingConfig(activeMode, new() { [activeMode] = mode ?? new TurnMode(ManualTurn: true) });
        var agent = new AgentConfig("agent", "project", null, "resume", "pack", new[] { "q" });
        return new AppConfig(session, turn, agent);
    }

    [Fact]
    public void Sets_model_modalities_and_pcm16_formats()
    {
        var options = SessionOptionsBuilder.Build(Cfg(), "test instructions");

        Assert.Equal("gpt-realtime", options.Model);
        Assert.Contains(InteractionModality.Text, options.Modalities);
        Assert.Contains(InteractionModality.Audio, options.Modalities);
        Assert.Equal(InputAudioFormat.Pcm16, options.InputAudioFormat);
        Assert.Equal(OutputAudioFormat.Pcm16, options.OutputAudioFormat);
        Assert.Equal(24000, options.InputAudioSamplingRate);
        Assert.Equal("test instructions", options.Instructions);
    }

    [Fact]
    public void Agent_options_omit_model_and_instructions_but_keep_common_audio_voice_and_turn_options()
    {
        var mode = new TurnMode(TurnDetection: new TurnDetectionConfig(
            "azure_semantic_vad",
            Threshold: 0.55,
            PrefixPaddingMs: 420,
            SilenceDurationMs: 600));

        var options = SessionOptionsBuilder.BuildForAgent(Cfg(activeMode: "open-mic", mode: mode));

        Assert.Null(options.Model);
        Assert.Null(options.Instructions);
        Assert.IsType<AzureStandardVoice>(options.Voice);
        Assert.IsType<AzureSemanticVadTurnDetection>(options.TurnDetection);
        Assert.Contains(InteractionModality.Text, options.Modalities);
        Assert.Contains(InteractionModality.Audio, options.Modalities);
        Assert.Equal(InputAudioFormat.Pcm16, options.InputAudioFormat);
        Assert.Equal(OutputAudioFormat.Pcm16, options.OutputAudioFormat);
        Assert.Equal(24000, options.InputAudioSamplingRate);
    }

    [Theory]
    [InlineData("azure-standard")]
    [InlineData("azure-realtime-native")]
    public void Maps_azure_standard_compatible_voice_types(string type)
    {
        var options = SessionOptionsBuilder.Build(Cfg(new VoiceConfig(type, "en-US-AvaNeural", 0.7, "+5%", "cheerful")), "i");

        var voice = Assert.IsType<AzureStandardVoice>(options.Voice);
        Assert.Equal("en-US-AvaNeural", voice.Name);
        Assert.Equal(0.7f, voice.Temperature);
        Assert.Equal("+5%", voice.Rate);
        Assert.Equal("cheerful", voice.Style);
    }

    [Fact]
    public void Maps_openai_voice()
    {
        var options = SessionOptionsBuilder.Build(Cfg(new VoiceConfig("openai", "alloy")), "i");

        var voice = Assert.IsType<OpenAIVoice>(options.Voice);
        Assert.Equal("alloy", voice.Name.ToString());
    }

    [Fact]
    public void Azure_custom_voice_is_rejected_until_endpoint_id_is_configured()
    {
        var ex = Assert.Throws<ConfigValidationException>(() =>
            SessionOptionsBuilder.Build(Cfg(new VoiceConfig("azure-custom", "custom")), "i"));

        Assert.Contains("azure-custom", ex.Message);
        Assert.Contains("not supported", ex.Message);
    }

    [Fact]
    public void Gated_manual_mode_uses_no_turn_detection()
    {
        var options = SessionOptionsBuilder.Build(Cfg(mode: new TurnMode(ManualTurn: true)), "i");

        Assert.Null(options.TurnDetection);
    }

    [Fact]
    public void Maps_azure_semantic_vad_with_eou()
    {
        var mode = new TurnMode(TurnDetection: new TurnDetectionConfig(
            "azure_semantic_vad",
            Threshold: 0.55,
            PrefixPaddingMs: 420,
            SilenceDurationMs: 600,
            InterruptResponse: true,
            EndOfUtteranceDetection: new VoiceLive.Cli.Config.EouDetection("semantic_detection_v1", "medium", 1200)));

        var options = SessionOptionsBuilder.Build(Cfg(activeMode: "open-mic", mode: mode), "i");

        var vad = Assert.IsType<AzureSemanticVadTurnDetection>(options.TurnDetection);
        Assert.Equal(0.55f, vad.Threshold);
        Assert.Equal(TimeSpan.FromMilliseconds(420), vad.PrefixPadding);
        Assert.Equal(TimeSpan.FromMilliseconds(600), vad.SilenceDuration);
        Assert.True(vad.InterruptResponse);
        var eou = Assert.IsType<AzureSemanticEouDetection>(vad.EndOfUtteranceDetection);
        Assert.Equal("medium", eou.ThresholdLevel?.ToString());
        Assert.Equal(TimeSpan.FromMilliseconds(1200), eou.Timeout);
    }
}
