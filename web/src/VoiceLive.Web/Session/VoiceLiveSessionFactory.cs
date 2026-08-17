using Azure.AI.VoiceLive;
using Azure.Core;
using VoiceLive.Web.Config;

namespace VoiceLive.Web.Session;

internal sealed class VoiceLiveSessionFactory(
    ServerSessionConfig config,
    TokenCredential credential,
    string modelInstructions,
    ILogger logger)
{
    internal async Task<VoiceLiveSession> CreateAsync(CancellationToken ct)
    {
        var serviceVersion = VoiceLiveServiceVersionMapper.Map(config.ApiVersion);
        var client = new VoiceLiveClient(
            new Uri(config.Endpoint),
            credential,
            new VoiceLiveClientOptions(serviceVersion));

        VoiceLiveSession session;
        if (config.Mode == "agent")
        {
            logger.LogInformation("Starting Voice Live session in AGENT mode ({Agent} / {Project})", config.Agent.AgentName, config.Agent.AgentProjectName);
            var agent = new AgentSessionConfig(config.Agent.AgentName, config.Agent.AgentProjectName);
            session = await client.StartSessionAsync(SessionTarget.FromAgent(agent), ct);
        }
        else
        {
            logger.LogInformation("Starting Voice Live session in MODEL mode ({Model})", config.Model);
            session = await client.StartSessionAsync(config.Model, ct);
        }

        try
        {
            var options = SessionOptionsBuilder.BuildForMode(config, modelInstructions);
            if (options is not null)
                await session.ConfigureSessionAsync(options, ct);

            return session;
        }
        catch
        {
            await session.DisposeAsync();
            throw;
        }
    }
}