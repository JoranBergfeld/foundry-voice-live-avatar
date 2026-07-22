namespace VoiceLive.Web.Session;

/// <summary>Stable browser wire frame announcing an agent tool/function/MCP event (diagnostic only).</summary>
public sealed record ToolNotification(string Phase, string? Name, string? CallId)
{
    public string T => "tool";
}
