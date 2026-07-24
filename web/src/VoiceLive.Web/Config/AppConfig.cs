using VoiceLive.Web.Session;

namespace VoiceLive.Web.Config;

public sealed record AppConfig(ServerSessionConfig Server, ClientConfig Client, string ModelInstructions);

public static class AppConfigLoader
{
    public static AppConfig Load(string dir, VoiceLiveOptions env)
    {
        var errors = new List<string>();
        var mode = SessionModeResolver.Resolve(env.Mode, Environment.GetEnvironmentVariable("VOICELIVE_MODE"));

        if (string.IsNullOrWhiteSpace(env.Endpoint))
            errors.Add("VoiceLive:Endpoint: is required (set app setting VoiceLive__Endpoint)");

        try
        {
            VoiceLiveServiceVersionMapper.Map(env.ApiVersion);
        }
        catch (WebConfigValidationException ex)
        {
            errors.Add(ex.Message);
        }

        var (server, client) = WebConfigLoader.BuildProjections(dir, env, mode, errors);

        var instructions = "";
        var groundingPath = Path.Combine(dir, env.GroundingFile);
        if (mode == SessionModeResolver.Model)
        {
            if (File.Exists(groundingPath)) instructions = File.ReadAllText(groundingPath);
            else errors.Add($"grounding: file not found at {groundingPath} (required in model mode)");
        }

        if (errors.Count > 0)
            throw new WebConfigValidationException("Configuration is invalid:\n  - " + string.Join("\n  - ", errors));

        return new AppConfig(server!, client!, instructions);
    }
}
