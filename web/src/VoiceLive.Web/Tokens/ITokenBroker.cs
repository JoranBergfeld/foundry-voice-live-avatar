namespace VoiceLive.Web.Tokens;

public sealed record AccessTokenResult(string Token, DateTimeOffset ExpiresOn);

public interface ITokenBroker
{
    Task<AccessTokenResult> GetTokenAsync(CancellationToken ct);
}
