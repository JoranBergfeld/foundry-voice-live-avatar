using System.Reflection;
using VoiceLive.Web.Session;
using Xunit;

public class AvatarCapacityErrorTests
{
    [Theory]
    [InlineData("avatar_service_resource_exhausted", true)]
    [InlineData("AVATAR_SERVICE_RESOURCE_EXHAUSTED", true)]
    [InlineData("avatar_capacity_exceeded", true)]
    [InlineData("rate_limit_exceeded", false)]
    [InlineData("server_error", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsAvatarCapacityError_classifies_avatar_capacity_signals(string? signal, bool expected)
    {
        var method = typeof(VoiceLiveWebSocketBridge).GetMethod(
            "IsAvatarCapacityError",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);

        var result = (bool)method!.Invoke(null, new object?[] { signal })!;

        Assert.Equal(expected, result);
    }
}
