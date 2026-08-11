using Azure.Core;
using Azure.Identity;
using MongoDB.Driver;
using MongoDB.Driver.Authentication.Oidc;

namespace Banking.Services;

public static class DocumentDbClientFactory
{
    private static readonly string[] Scopes = ["https://ossrdbms-aad.database.windows.net/.default"];

    public static MongoClient Create(string clusterName, string? managedIdentityClientId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clusterName);

        DefaultAzureCredential credential = string.IsNullOrWhiteSpace(managedIdentityClientId)
            ? new DefaultAzureCredential()
            : new DefaultAzureCredential(new DefaultAzureCredentialOptions
            {
                ManagedIdentityClientId = managedIdentityClientId
            });

        MongoClientSettings settings = MongoClientSettings.FromConnectionString(
            $"mongodb+srv://{clusterName}.global.mongocluster.cosmos.azure.com/");
        settings.UseTls = true;
        settings.RetryWrites = false;
        settings.MaxConnectionIdleTime = TimeSpan.FromMinutes(2);
        settings.Credential = MongoCredential.CreateOidcCredential(new AzureIdentityTokenHandler(credential));

        return new MongoClient(settings);
    }

    private sealed class AzureIdentityTokenHandler(TokenCredential credential) : IOidcCallback
    {
        public OidcAccessToken GetOidcAccessToken(
            OidcCallbackParameters parameters,
            CancellationToken cancellationToken)
        {
            AccessToken token = credential.GetToken(new TokenRequestContext(Scopes), cancellationToken);
            return new OidcAccessToken(token.Token, token.ExpiresOn - DateTimeOffset.UtcNow);
        }

        public async Task<OidcAccessToken> GetOidcAccessTokenAsync(
            OidcCallbackParameters parameters,
            CancellationToken cancellationToken)
        {
            AccessToken token = await credential.GetTokenAsync(
                new TokenRequestContext(Scopes),
                cancellationToken);
            return new OidcAccessToken(token.Token, token.ExpiresOn - DateTimeOffset.UtcNow);
        }
    }
}