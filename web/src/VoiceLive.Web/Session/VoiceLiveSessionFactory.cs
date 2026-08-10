using Azure.AI.VoiceLive;
using Azure.Core;
using VoiceLive.Web.Config;

namespace VoiceLive.Web.Session;

internal sealed class VoiceLiveSessionFactory(
    ServerSessionConfig config,
    TokenCredential credential,
    string modelInstructions,
    HostedAgentSessionOptionsProvider hostedAgentOptions,
    ILogger logger)
{
    internal async Task<VoiceLiveSession> CreateAsync(CancellationToken ct)
    {
        var serviceVersion = VoiceLiveServiceVersionMapper.Map(config.ApiVersion);
        var client = new VoiceLiveClient(
            new Uri(config.Endpoint),
            credential,
            new VoiceLiveClientOptions(serviceVersion));

        VoiceLiveSession? session = null;
        try
        {
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

            var sessionOptions = config.Mode == SessionModeResolver.Agent
                ? await hostedAgentOptions.LoadAsync(new Uri(config.Endpoint), config.Agent, ct)
                : SessionOptionsBuilder.Build(config, modelInstructions);
            await session.ConfigureSessionAsync(sessionOptions, ct);

            return session;
        }
        catch
        {
            if (session is not null)
                await session.DisposeAsync();
            throw;
        }
    }
}