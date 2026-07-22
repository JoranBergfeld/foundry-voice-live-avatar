using Azure.AI.VoiceLive;
using VoiceLive.Cli.Config;

namespace VoiceLive.Cli.Session;

public static class SessionOptionsBuilder
{
    public static VoiceLiveSessionOptions Build(AppConfig config, string instructions)
    {
        var session = config.Session;
        var options = new VoiceLiveSessionOptions
        {
            Model = session.Model,
            Instructions = instructions,
            Voice = BuildVoice(session.Voice),
            TurnDetection = BuildTurnDetection(config.TurnTaking),
            InputAudioFormat = InputAudioFormat.Pcm16,
            OutputAudioFormat = OutputAudioFormat.Pcm16,
            InputAudioSamplingRate = session.InputAudioSamplingRate,
        };

        options.Modalities.Clear();
        options.Modalities.Add(InteractionModality.Text);
        options.Modalities.Add(InteractionModality.Audio);
        return options;
    }

    private static VoiceProvider BuildVoice(VoiceConfig voice) => voice.Type switch
    {
        "azure-standard" or "azure-realtime-native" => BuildAzureStandardVoice(voice),
        "openai" => new OpenAIVoice(new OAIVoice(voice.Name)),
        "azure-custom" => throw new ConfigValidationException("session.json: voice.type 'azure-custom' is not supported yet because config has no custom voice endpoint id."),
        _ => throw new ConfigValidationException($"session.json: voice.type '{voice.Type}' is not supported.")
    };

    private static AzureStandardVoice BuildAzureStandardVoice(VoiceConfig voice)
    {
        var azureVoice = new AzureStandardVoice(voice.Name);
        if (voice.Rate is not null) azureVoice.Rate = voice.Rate;
        if (voice.Style is not null) azureVoice.Style = voice.Style;
        if (voice.Temperature is not null) azureVoice.Temperature = (float)voice.Temperature.Value;
        return azureVoice;
    }

    private static TurnDetection BuildTurnDetection(TurnTakingConfig turnTaking)
    {
        var mode = turnTaking.Modes[turnTaking.ActiveMode];
        if (mode.ManualTurn) return new NoTurnDetection();
        if (mode.TurnDetection is null) return new NoTurnDetection();

        return mode.TurnDetection.Type switch
        {
            "azure_semantic_vad" => BuildAzureSemanticVad(mode.TurnDetection),
            "server_vad" => BuildServerVad(mode.TurnDetection),
            _ => throw new ConfigValidationException($"turntaking.json: turnDetection.type '{mode.TurnDetection.Type}' is not supported.")
        };
    }

    private static AzureSemanticVadTurnDetection BuildAzureSemanticVad(TurnDetectionConfig config)
    {
        var vad = new AzureSemanticVadTurnDetection();
        ApplyCommonVad(vad, config);
        return vad;
    }

    private static ServerVadTurnDetection BuildServerVad(TurnDetectionConfig config)
    {
        var vad = new ServerVadTurnDetection();
        ApplyCommonVad(vad, config);
        return vad;
    }

    private static void ApplyCommonVad(TurnDetection vad, TurnDetectionConfig config)
    {
        switch (vad)
        {
            case AzureSemanticVadTurnDetection semantic:
                if (config.Threshold is not null) semantic.Threshold = (float)config.Threshold.Value;
                if (config.PrefixPaddingMs is not null) semantic.PrefixPadding = TimeSpan.FromMilliseconds(config.PrefixPaddingMs.Value);
                if (config.SilenceDurationMs is not null) semantic.SilenceDuration = TimeSpan.FromMilliseconds(config.SilenceDurationMs.Value);
                if (config.InterruptResponse is not null) semantic.InterruptResponse = config.InterruptResponse.Value;
                if (config.EndOfUtteranceDetection is not null) semantic.EndOfUtteranceDetection = BuildEou(config.EndOfUtteranceDetection);
                break;
            case ServerVadTurnDetection server:
                if (config.Threshold is not null) server.Threshold = (float)config.Threshold.Value;
                if (config.PrefixPaddingMs is not null) server.PrefixPadding = TimeSpan.FromMilliseconds(config.PrefixPaddingMs.Value);
                if (config.SilenceDurationMs is not null) server.SilenceDuration = TimeSpan.FromMilliseconds(config.SilenceDurationMs.Value);
                if (config.InterruptResponse is not null) server.InterruptResponse = config.InterruptResponse.Value;
                if (config.EndOfUtteranceDetection is not null) server.EndOfUtteranceDetection = BuildEou(config.EndOfUtteranceDetection);
                break;
        }
    }

    private static AzureSemanticEouDetection BuildEou(VoiceLive.Cli.Config.EouDetection config)
    {
        var eou = new AzureSemanticEouDetection();
        if (config.ThresholdLevel is not null) eou.ThresholdLevel = new EouThresholdLevel(config.ThresholdLevel);
        if (config.TimeoutMs is not null) eou.Timeout = TimeSpan.FromMilliseconds(config.TimeoutMs.Value);
        return eou;
    }
}
