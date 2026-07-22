using Azure.Core;
using Azure.Identity;

namespace VoiceLive.Web.Tokens;

public sealed class EntraTokenBroker : ITokenBroker
{
    private static readonly TokenRequestContext RequestContext = new(["https://ai.azure.com/.default"]);
    private readonly DefaultAzureCredential _credential = new();

    public async Task<AccessTokenResult> GetTokenAsync(CancellationToken ct)
    {
        try
        {
            var token = await _credential.GetTokenAsync(RequestContext, ct);
            return new AccessTokenResult(token.Token, token.ExpiresOn);
        }
        catch (CredentialUnavailableException)
        {
            throw new TokenBrokerException("No Azure credential available; run `az login` or configure a managed identity.");
        }
        catch (AuthenticationFailedException)
        {
            throw new TokenBrokerException("No Azure credential available; run `az login` or configure a managed identity.");
        }
    }
}
