using Microsoft.Extensions.Diagnostics.HealthChecks;
using VoiceLive.Web.Config;

namespace VoiceLive.Web.Health;

public sealed class ConfigHealthCheck(ConfigState state) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext ctx, CancellationToken ct)
        => Task.FromResult(state.Config is not null
            ? HealthCheckResult.Healthy("config loaded")
            : HealthCheckResult.Unhealthy(state.Error ?? "config missing"));
}
