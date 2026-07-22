using System.Text.Json;
using VoiceLive.Web.Session;
using Xunit;

public class ToolNotificationTests
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    [Fact]
    public void Serializes_to_stable_tool_frame()
    {
        var json = JsonSerializer.Serialize(new ToolNotification("done", "get_weather", "call_1"), Web);

        Assert.Contains("\"t\":\"tool\"", json);
        Assert.Contains("\"phase\":\"done\"", json);
        Assert.Contains("\"name\":\"get_weather\"", json);
        Assert.Contains("\"callId\":\"call_1\"", json);
    }

    [Fact]
    public void Allows_null_name_and_callId()
    {
        var json = JsonSerializer.Serialize(new ToolNotification("list", null, "item_9"), Web);
        Assert.Contains("\"phase\":\"list\"", json);
        Assert.Contains("\"name\":null", json);
        Assert.Contains("\"callId\":\"item_9\"", json);
    }
}
