using VoiceLive.Cli.Config;
using Xunit;

public class ConfigLoaderTests
{
    private static string WriteTemp(Dictionary<string,string> files)
    {
        var dir = Path.Combine(AppContext.BaseDirectory, "TestScratch", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        Directory.CreateDirectory(Path.Combine(dir, "grounding"));
        foreach (var (name, content) in files) File.WriteAllText(Path.Combine(dir, name), content);
        return dir;
    }

    private static Dictionary<string,string> Valid() => new()
    {
        ["session.json"] = """
        {"endpoint":"wss://x.services.ai.azure.com","region":"swedencentral","apiVersion":"2026-04-10","model":"gpt-realtime",
         "voice":{"type":"azure-realtime-native","name":"andrew"},"inputAudioSamplingRate":24000,
         "inputAudioNoiseReduction":{"type":"azure_deep_noise_suppression"},
         "inputAudioEchoCancellation":{"type":"server_echo_cancellation"},
         "inputAudioTranscription":{"model":"azure-speech","language":"en"}}
        """,
        ["turntaking.json"] = """
        {"activeMode":"gated","modes":{"gated":{"manualTurn":true,"interruptResponse":false}}}
        """,
        ["agent.json"] = """
        {"agentName":"a","agentProjectName":"p","agentVersion":null,"conversationResumePolicy":"resume",
         "groundingStrategy":"pack","safeQuestions":["q1"]}
        """,
        ["avatar.json"] = """
        {"character":"lisa","style":"casual-sitting","customized":false,
         "video":{"resolution":{"width":1920,"height":1080},"bitrate":2000000,"codec":"h264"}}
        """
    };

    [Fact]
    public void Loads_valid_config()
    {
        var dir = WriteTemp(Valid());
        var cfg = ConfigLoader.Load(dir);
        Assert.Equal("swedencentral", cfg.Session.Region);
        Assert.Equal("gated", cfg.TurnTaking.ActiveMode);
        Assert.Equal("a", cfg.Agent.AgentName);
    }

    [Fact]
    public void Fails_when_active_mode_missing_from_modes()
    {
        var files = Valid();
        files["turntaking.json"] = """{"activeMode":"open-mic","modes":{"gated":{"manualTurn":true}}}""";
        var ex = Assert.Throws<ConfigValidationException>(() => ConfigLoader.Load(WriteTemp(files)));
        Assert.Contains("turntaking.json", ex.Message);
        Assert.Contains("open-mic", ex.Message);
    }

    [Fact]
    public void Fails_on_unknown_voice_type()
    {
        var files = Valid();
        files["session.json"] = files["session.json"].Replace("azure-realtime-native", "bogus-voice");
        var ex = Assert.Throws<ConfigValidationException>(() => ConfigLoader.Load(WriteTemp(files)));
        Assert.Contains("session.json", ex.Message);
        Assert.Contains("voice.type", ex.Message);
    }

    [Fact]
    public void Fails_when_required_session_object_missing()
    {
        var files = Valid();
        files["session.json"] = """
        {"endpoint":"wss://x.services.ai.azure.com","region":"swedencentral","apiVersion":"2026-04-10","model":"gpt-realtime",
         "voice":{"type":"azure-realtime-native","name":"andrew"},"inputAudioSamplingRate":24000,
         "inputAudioTranscription":{"model":"azure-speech","language":"en"}}
        """;
        var ex = Assert.Throws<ConfigValidationException>(() => ConfigLoader.Load(WriteTemp(files)));
        Assert.Contains("session.json", ex.Message);
        Assert.Contains("inputAudioNoiseReduction", ex.Message);
        Assert.Contains("inputAudioEchoCancellation", ex.Message);
    }

    [Fact]
    public void Fails_with_missing_file_naming_the_file()
    {
        var files = Valid(); files.Remove("agent.json");
        var ex = Assert.Throws<ConfigValidationException>(() => ConfigLoader.Load(WriteTemp(files)));
        Assert.Contains("agent.json", ex.Message);
    }
}
