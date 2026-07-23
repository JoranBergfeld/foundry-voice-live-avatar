using VoiceLive.Web.Session;
using Xunit;

public class SessionGateTests
{
    [Fact]
    public void Blocks_when_capacity_reached()
    {
        var gate = new SessionGate(2);
        Assert.True(gate.TryEnter());
        Assert.True(gate.TryEnter());
        Assert.False(gate.TryEnter());
        gate.Exit();
        Assert.True(gate.TryEnter());
    }
}
