using System.Reflection;
using System.Text;
using System.Text.Json;
using VoiceLive.Web.Session;
using Xunit;

public class AvatarSdpCodecTests
{
    [Fact]
    public void EncodeAvatarOffer_wraps_raw_sdp_as_base64_json_offer()
    {
        var rawOffer = "v=0\r\no=- 1 2 IN IP4 127.0.0.1\r\n";
        var encoded = InvokePrivateStatic<string>("EncodeAvatarOffer", rawOffer);

        var json = Encoding.UTF8.GetString(Convert.FromBase64String(encoded));
        using var doc = JsonDocument.Parse(json);
        Assert.Equal("offer", doc.RootElement.GetProperty("type").GetString());
        Assert.Equal(rawOffer, doc.RootElement.GetProperty("sdp").GetString());
    }

    [Fact]
    public void DecodeAvatarAnswer_extracts_raw_sdp_from_base64_json_answer()
    {
        var rawAnswer = "v=0\r\no=- 3 4 IN IP4 127.0.0.1\r\n";
        var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { type = "answer", sdp = rawAnswer })));

        var decoded = InvokePrivateStatic<string?>("DecodeAvatarAnswer", encoded);

        Assert.Equal(rawAnswer, decoded);
    }

    [Fact]
    public void DecodeAvatarAnswer_returns_null_for_invalid_payload()
    {
        var decoded = InvokePrivateStatic<string?>("DecodeAvatarAnswer", "not-base64-json");

        Assert.Null(decoded);
    }

    private static T InvokePrivateStatic<T>(string name, params object?[] args)
    {
        var method = typeof(VoiceLiveWebSocketBridge).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);
        return (T)method!.Invoke(null, args)!;
    }
}
