using VoiceLive.Cli.Config;

namespace VoiceLive.Cli.Session;

public static class SessionPayloadBuilder
{
    public static Dictionary<string, object?> Build(AppConfig cfg)
    {
        var s = cfg.Session;
        var payload = new Dictionary<string, object?>
        {
            ["modalities"] = new[] { "text", "audio" },
            ["voice"] = Prune(new Dictionary<string, object?>
            {
                ["type"] = s.Voice.Type, ["name"] = s.Voice.Name,
                ["temperature"] = s.Voice.Temperature, ["rate"] = s.Voice.Rate, ["style"] = s.Voice.Style
            }),
            ["input_audio_sampling_rate"] = s.InputAudioSamplingRate,
            ["input_audio_noise_reduction"] = new Dictionary<string, object?> { ["type"] = s.InputAudioNoiseReduction.Type },
            ["input_audio_echo_cancellation"] = new Dictionary<string, object?> { ["type"] = s.InputAudioEchoCancellation.Type },
            ["input_audio_transcription"] = Prune(new Dictionary<string, object?>
            {
                ["model"] = s.InputAudioTranscription.Model, ["language"] = s.InputAudioTranscription.Language
            })
        };

        var mode = cfg.TurnTaking.Modes[cfg.TurnTaking.ActiveMode];
        if (!mode.ManualTurn && mode.TurnDetection is { } td)
            payload["turn_detection"] = BuildTurnDetection(td);

        return payload;
    }

    private static Dictionary<string, object?> BuildTurnDetection(TurnDetectionConfig td)
    {
        var d = new Dictionary<string, object?>
        {
            ["type"] = td.Type,
            ["threshold"] = td.Threshold,
            ["prefix_padding_ms"] = td.PrefixPaddingMs,
            ["silence_duration_ms"] = td.SilenceDurationMs,
            ["interrupt_response"] = td.InterruptResponse
        };
        if (td.EndOfUtteranceDetection is { } e)
            d["end_of_utterance_detection"] = Prune(new Dictionary<string, object?>
            {
                ["model"] = e.Model, ["threshold_level"] = e.ThresholdLevel, ["timeout_ms"] = e.TimeoutMs
            });
        return Prune(d);
    }

    private static Dictionary<string, object?> Prune(Dictionary<string, object?> d)
        => d.Where(kv => kv.Value is not null).ToDictionary(kv => kv.Key, kv => kv.Value);
}
