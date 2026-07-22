using Azure.AI.VoiceLive;
using VoiceLive.Web.Config;
using VoiceLive.Web.Session;
using Xunit;

public class ServerSessionConfigTests
{
    private static string RepoConfigDir => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "..", "config"));

    [Fact]
    public void LoadServerSession_returns_endpoint_and_active_turn_mode()
    {
        var config = WebConfigLoader.LoadServerSession(RepoConfigDir);

        Assert.Equal("https://testlab-f.services.ai.azure.com", config.Endpoint);
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
        var config = WebConfigLoader.LoadServerSession(RepoConfigDir);

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
        var config = WebConfigLoader.LoadServerSession(RepoConfigDir);
        Assert.Equal("model", config.Mode);
    }
}
