using System.Text.Json;
using VoiceLive.Cli.Config;
using VoiceLive.Cli.Session;

namespace VoiceLive.Cli;

public static class Program
{
    public static int Main(string[] args) => Run(args, Console.Out, Console.Error);

    public static int Run(string[] args, TextWriter outw, TextWriter errw)
    {
        if (args.Length == 0 || args[0] != "validate")
        {
            errw.WriteLine("usage: voicelive-cli validate --config <dir>");
            return 2;
        }
        var dir = ArgValue(args, "--config") ?? "config";
        try
        {
            var cfg = ConfigLoader.Load(dir);
            var payload = SessionPayloadBuilder.Build(cfg);
            outw.WriteLine($"Config OK. Active turn-taking mode: {cfg.TurnTaking.ActiveMode}");
            outw.WriteLine("Resolved session.update payload:");
            outw.WriteLine(JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true }));
            return 0;
        }
        catch (ConfigValidationException ex)
        {
            errw.WriteLine(ex.Message);
            return 1;
        }
    }

    private static string? ArgValue(string[] args, string name)
    {
        var i = Array.IndexOf(args, name);
        return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
    }
}
