namespace VoiceLive.Web.Config;

public static class SessionModeResolver
{
    public const string Model = "model";
    public const string Agent = "agent";

    public static bool IsValid(string? value)
        => value is not null && Normalize(value) is Model or Agent;

    public static string Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? Model : value.Trim().ToLowerInvariant();

    /// <summary>Env override wins over the config value; both are validated. Invalid values throw.</summary>
    public static string Resolve(string? configMode, string? envMode)
    {
        var chosen = !string.IsNullOrWhiteSpace(envMode) ? envMode : configMode;
        if (string.IsNullOrWhiteSpace(chosen)) return Model;
        if (!IsValid(chosen))
            throw new WebConfigValidationException(
                $"session mode '{chosen}' is invalid; expected '{Model}' or '{Agent}' (from session.json 'mode' or VOICELIVE_MODE).");
        return Normalize(chosen);
    }
}
