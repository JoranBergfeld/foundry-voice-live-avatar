using VoiceLive.Web.Session;
using Xunit;

public class ControlMessageTests
{
    [Theory]
    [InlineData("{\"t\":\"ping\"}", true, "ping")]
    [InlineData("not json", false, null)]
    [InlineData("{\"x\":1}", true, null)]
    public void Parses_control_type(string json, bool ok, string? expected)
    {
        var result = VoiceLiveWebSocketBridge.TryGetControlType(json, out var type);
        Assert.Equal(ok, result);
        Assert.Equal(expected, type);
    }
}
