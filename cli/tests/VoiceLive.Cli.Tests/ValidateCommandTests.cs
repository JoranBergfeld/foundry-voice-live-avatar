using VoiceLive.Cli;
using Xunit;

public class ValidateCommandTests
{
    [Fact]
    public void Validate_on_repo_config_returns_zero()
    {
        var repoConfig = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "..", "config");
        var outw = new StringWriter(); var errw = new StringWriter();
        var code = Program.Run(["validate", "--config", repoConfig], outw, errw);
        Assert.Equal(0, code);
        Assert.Contains("Config OK", outw.ToString());
    }

    [Fact]
    public void Validate_on_bad_dir_returns_one()
    {
        var outw = new StringWriter(); var errw = new StringWriter();
        var code = Program.Run(["validate", "--config", "/no/such/dir"], outw, errw);
        Assert.Equal(1, code);
        Assert.Contains("session.json", errw.ToString());
    }
}
