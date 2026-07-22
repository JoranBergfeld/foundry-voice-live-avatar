namespace VoiceLive.Cli.Config;

public sealed class ConfigValidationException(string message) : Exception(message);
