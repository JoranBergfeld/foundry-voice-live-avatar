namespace VoiceLive.Cli.Config;

public sealed record VoiceConfig(string Type, string Name, double? Temperature = null, string? Rate = null, string? Style = null);
public sealed record NoiseReduction(string Type);
public sealed record EchoCancellation(string Type);
public sealed record Transcription(string Model, string? Language = null);

public sealed record SessionConfig(
    string Endpoint,
    string Region,
    string ApiVersion,
    string Model,
    VoiceConfig Voice,
    int InputAudioSamplingRate,
    NoiseReduction InputAudioNoiseReduction,
    EchoCancellation InputAudioEchoCancellation,
    Transcription InputAudioTranscription);

public sealed record EouDetection(string Model, string? ThresholdLevel = null, int? TimeoutMs = null);
public sealed record TurnDetectionConfig(
    string Type,
    double? Threshold = null,
    int? PrefixPaddingMs = null,
    int? SilenceDurationMs = null,
    bool? InterruptResponse = null,
    EouDetection? EndOfUtteranceDetection = null);

public sealed record TurnMode(
    bool ManualTurn = false,
    bool? GateGatesBargeIn = null,
    bool? InterruptResponse = null,
    TurnDetectionConfig? TurnDetection = null);

public sealed record TurnTakingConfig(string ActiveMode, Dictionary<string, TurnMode> Modes);

public sealed record AgentConfig(
    string AgentName,
    string AgentProjectName,
    string? AgentVersion,
    string ConversationResumePolicy,
    string GroundingStrategy,
    IReadOnlyList<string> SafeQuestions);
