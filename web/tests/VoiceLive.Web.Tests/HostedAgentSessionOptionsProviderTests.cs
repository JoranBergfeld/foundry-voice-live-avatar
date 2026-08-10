using System.Net;
using System.Text;
using System.Text.Json;
using Azure.AI.VoiceLive;
using Azure.Core;
using VoiceLive.Web.Config;
using VoiceLive.Web.Session;

public sealed class HostedAgentSessionOptionsProviderTests
{
    [Fact]
    public async Task LoadAsync_uses_latest_chunked_hosted_media_configuration()
    {
        const string hostedConfig = """
            {
              "session": {
                "voice": {
                  "name": "en-au-cyanspark:DragonHDOmniLatestNeural",
                  "type": "azure-standard",
                  "temperature": 0.8,
                  "rate": "1"
                },
                "inputAudioTranscription": {
                  "model": "azure-speech",
                  "language": "auto-detect"
                },
                "turnDetection": {
                  "type": "azure_semantic_vad",
                  "removeFillerWords": true
                },
                "avatar": {
                  "character": "nia",
                  "style": "",
                  "customized": false
                }
              }
            }
            """;
        var splitAt = hostedConfig.Length / 2;
        var response = JsonSerializer.Serialize(new
        {
            data = new object[]
            {
                new { version = "4", metadata = new Dictionary<string, string>() },
                new
                {
                    version = "5",
                    metadata = new Dictionary<string, string>
                    {
                        ["microsoft.voice-live.configuration"] = JsonSerializer.Serialize(new { chunked = true, totalChunks = 2 }),
                        ["microsoft.voice-live.configuration.chunk_1"] = hostedConfig[..splitAt],
                        ["microsoft.voice-live.configuration.chunk_2"] = hostedConfig[splitAt..]
                    }
                }
            }
        });
        var handler = new RecordingHandler(response);
        var provider = new HostedAgentSessionOptionsProvider(new HttpClient(handler), new StubCredential());

        var options = await provider.LoadAsync(
            new Uri("https://example.services.ai.azure.com"),
            new ServerAgentConfig("company direction/avatar", "project one", []),
            CancellationToken.None);

        var voice = Assert.IsType<AzureStandardVoice>(options.Voice);
        Assert.Equal("en-au-cyanspark:DragonHDOmniLatestNeural", voice.Name);
        Assert.Equal(0.8f, voice.Temperature);
        Assert.Equal("1", voice.Rate);
        Assert.Equal("nia", options.Avatar.Character);
        Assert.Null(options.Avatar.Style);
        Assert.IsType<AzureSemanticVadTurnDetection>(options.TurnDetection);
        Assert.Equal("azure-speech", options.InputAudioTranscription?.Model.ToString());
        Assert.Null(options.InputAudioTranscription?.Language);
        Assert.Null(options.Model);
        Assert.Null(options.Instructions);
        Assert.Empty(options.Tools);
        Assert.Equal(
            "/api/projects/project%20one/agents/company%20direction%2Favatar/versions?api-version=v1",
            handler.RequestUri?.PathAndQuery);
        Assert.Equal("Bearer", handler.AuthorizationScheme);
    }

    [Fact]
    public async Task LoadAsync_rejects_agent_without_voice_live_configuration()
    {
        var handler = new RecordingHandler("""{"data":[{"version":"1","metadata":{}}]}""");
        var provider = new HostedAgentSessionOptionsProvider(new HttpClient(handler), new StubCredential());

        var error = await Assert.ThrowsAsync<HostedAgentConfigurationException>(() => provider.LoadAsync(
            new Uri("https://example.services.ai.azure.com"),
            new ServerAgentConfig("agent", "project", []),
            CancellationToken.None));

        Assert.Contains("does not contain Voice Live configuration metadata", error.Message);
    }

    private sealed class RecordingHandler(string response) : HttpMessageHandler
    {
        public Uri? RequestUri { get; private set; }
        public string? AuthorizationScheme { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestUri = request.RequestUri;
            AuthorizationScheme = request.Headers.Authorization?.Scheme;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(response, Encoding.UTF8, "application/json")
            });
        }
    }

    private sealed class StubCredential : TokenCredential
    {
        public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken)
            => new("token", DateTimeOffset.MaxValue);

        public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken)
            => ValueTask.FromResult(new AccessToken("token", DateTimeOffset.MaxValue));
    }
}