using VoiceLive.Web.Config;
using Xunit;

public class SessionModeResolverTests
{
    [Fact]
    public void Defaults_to_model_when_both_absent()
        => Assert.Equal("model", SessionModeResolver.Resolve(configMode: null, envMode: null));

    [Fact]
    public void Uses_config_mode_when_env_absent()
        => Assert.Equal("agent", SessionModeResolver.Resolve("agent", null));

    [Fact]
    public void Env_overrides_config()
        => Assert.Equal("model", SessionModeResolver.Resolve("agent", "model"));

    [Theory]
    [InlineData(" Agent ")]
    [InlineData("AGENT")]
    public void Normalizes_case_and_whitespace(string value)
        => Assert.Equal("agent", SessionModeResolver.Resolve(value, null));

    [Fact]
    public void Invalid_config_mode_throws()
    {
        var ex = Assert.Throws<WebConfigValidationException>(() => SessionModeResolver.Resolve("hybrid", null));
        Assert.Contains("hybrid", ex.Message);
        Assert.Contains("model", ex.Message);
        Assert.Contains("agent", ex.Message);
    }

    [Fact]
    public void Invalid_env_mode_throws()
        => Assert.Throws<WebConfigValidationException>(() => SessionModeResolver.Resolve(null, "bogus"));

    [Fact]
    public void IsValid_and_Normalize_behave()
    {
        Assert.True(SessionModeResolver.IsValid("Agent"));
        Assert.False(SessionModeResolver.IsValid("nope"));
        Assert.Equal("model", SessionModeResolver.Normalize(null));
        Assert.Equal("agent", SessionModeResolver.Normalize(" AGENT "));
    }
}
