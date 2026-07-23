using Azure.AI.VoiceLive;
using VoiceLive.Web.Config;
using VoiceLive.Web.Session;
using Xunit;

public class ServerSessionConfigTests
{
    private static string RepoConfigDir => TestAppFactory.RepoConfigDir;
    private static VoiceLiveOptions ModelOpts() => new() { Endpoint = "https://x", Mode = "model", ApiVersion = "2025-10-01" };

    [Fact]
    public void LoadServerSession_returns_endpoint_and_active_turn_mode()
    {
        var config = AppConfigLoader.Load(RepoConfigDir, ModelOpts()).Server;

        Assert.Equal("https://x", config.Endpoint);
        Assert.Equal("2025-10-01", config.ApiVersion);
        Assert.Equal("gpt-realtime", config.Model);
        Assert.Equal("gated", config.TurnTaking.ActiveMode);
        Assert.True(config.TurnTaking.ActiveModeConfig.ManualTurn);
        Assert.Equal("lisa", config.Avatar.Character);
        Assert.Equal("casual-sitting", config.Avatar.Style);
        Assert.Equal(1920, config.Avatar.Video?.Resolution.Width);
    }

    [Fact]
    public void Build_maps_gated_avatar_session_to_verified_sdk_options()
    {
        var config = AppConfigLoader.Load(RepoConfigDir, ModelOpts()).Server;

        var options = SessionOptionsBuilder.Build(config, "Keep answers short.");

        Assert.Equal("gpt-realtime", options.Model);
        Assert.Equal("Keep answers short.", options.Instructions);
        Assert.IsType<AzureStandardVoice>(options.Voice);
        Assert.True(options.TurnDetection is null || options.TurnDetection is NoTurnDetection);
        Assert.Equal(InputAudioFormat.Pcm16, options.InputAudioFormat);
        Assert.Equal(OutputAudioFormat.Pcm16, options.OutputAudioFormat);
        Assert.Equal(24000, options.InputAudioSamplingRate);
        Assert.Null(options.InputAudioEchoCancellation);
        Assert.Null(options.InputAudioTranscription);
        Assert.Contains(options.Modalities, m => m.Equals(InteractionModality.Text));
        Assert.Contains(options.Modalities, m => m.Equals(InteractionModality.Audio));
        Assert.NotNull(options.Avatar);
        Assert.Equal("lisa", options.Avatar.Character);
        Assert.Equal("casual-sitting", options.Avatar.Style);
        Assert.Equal(2000000, options.Avatar.Video.Bitrate);
        Assert.Equal("h264", options.Avatar.Video.Codec);
        Assert.Equal(1920, options.Avatar.Video.Resolution.Width);
        Assert.Equal(1080, options.Avatar.Video.Resolution.Height);
    }

    [Fact]
    public void LoadServerSession_defaults_mode_to_model()
    {
        var config = AppConfigLoader.Load(RepoConfigDir, ModelOpts()).Server;
        Assert.Equal("model", config.Mode);
    }

    [Fact]
    public void AppConfigLoader_missing_endpoint_throws()
    {
        var ex = Assert.Throws<WebConfigValidationException>(() =>
            AppConfigLoader.Load(RepoConfigDir, new VoiceLiveOptions { Endpoint = "", Mode = "model" }));
        Assert.Contains("VoiceLive:Endpoint", ex.Message);
    }

    [Fact]
    public void AppConfigLoader_unknown_api_version_throws()
    {
        var ex = Assert.Throws<WebConfigValidationException>(() =>
            AppConfigLoader.Load(RepoConfigDir, new VoiceLiveOptions { Endpoint = "https://x", ApiVersion = "1999-01-01", Mode = "model" }));
        Assert.Contains("apiVersion '1999-01-01' is not supported", ex.Message);
    }

    [Fact]
    public void AppConfigLoader_agent_mode_allows_missing_model_and_grounding()
    {
        var config = AppConfigLoader.Load(RepoConfigDir, new VoiceLiveOptions { Endpoint = "https://x", Mode = "agent" });
        Assert.Equal("agent", config.Server.Mode);
    }

    [Fact]
    public void BuildForAgent_omits_model_and_instructions_but_keeps_voice_avatar_and_audio()
    {
        var config = AppConfigLoader.Load(RepoConfigDir, ModelOpts()).Server;

        var options = SessionOptionsBuilder.BuildForAgent(config);

        Assert.Null(options.Model);
        Assert.Null(options.Instructions);
        Assert.IsType<AzureStandardVoice>(options.Voice);
        Assert.Equal(InputAudioFormat.Pcm16, options.InputAudioFormat);
        Assert.Equal(OutputAudioFormat.Pcm16, options.OutputAudioFormat);
        Assert.Equal(24000, options.InputAudioSamplingRate);
        Assert.NotNull(options.Avatar);
        Assert.Equal("lisa", options.Avatar.Character);
        Assert.Equal("casual-sitting", options.Avatar.Style);
        Assert.Contains(options.Modalities, m => m.Equals(InteractionModality.Text));
        Assert.Contains(options.Modalities, m => m.Equals(InteractionModality.Audio));
    }
}
