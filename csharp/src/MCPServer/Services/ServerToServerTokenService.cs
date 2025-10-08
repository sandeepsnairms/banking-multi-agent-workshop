using MCPServer.Models;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace MCPServer.Services;

public interface IServerToServerTokenService
{
    Task<string> GetTokenAsync(string clientId, string clientSecret, string[]? scopes = null);
    Task<bool> ValidateTokenAsync(string token);
}

public class ServerToServerTokenService : IServerToServerTokenService
{
    private readonly HttpClient _httpClient;
    private readonly OAuthSettings _oauthSettings;
    private readonly ILogger<ServerToServerTokenService> _logger;
    private readonly string _baseUrl;

    public ServerToServerTokenService(
        HttpClient httpClient,
        IOptions<OAuthSettings> oauthSettings,
        IConfiguration configuration,
        ILogger<ServerToServerTokenService> logger)
    {
        _httpClient = httpClient;
        _oauthSettings = oauthSettings.Value;
        _logger = logger;
        _baseUrl = configuration["ServerUrl"] ?? "http://localhost:7071/";
    }

    public async Task<string> GetTokenAsync(string clientId, string clientSecret, string[]? scopes = null)
    {
        try
        {
            var tokenEndpoint = $"{_baseUrl.TrimEnd('/')}/oauth/token";
            var requestScopes = scopes ?? new[] { "mcp:tools" };

            var requestData = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("grant_type", "client_credentials"),
                new KeyValuePair<string, string>("client_id", clientId),
                new KeyValuePair<string, string>("client_secret", clientSecret),
                new KeyValuePair<string, string>("scope", string.Join(" ", requestScopes))
            });

            _logger.LogInformation("Requesting server-to-server token for client: {ClientId}", clientId);

            var response = await _httpClient.PostAsync(tokenEndpoint, requestData);
            var responseContent = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                var tokenResponse = JsonSerializer.Deserialize<OAuthTokenResponse>(responseContent);
                if (tokenResponse != null)
                {
                    _logger.LogInformation("Successfully obtained token for client: {ClientId}", clientId);
                    return tokenResponse.AccessToken;
                }
            }

            _logger.LogError("Failed to obtain token for client {ClientId}. Response: {Response}", 
                clientId, responseContent);
            throw new InvalidOperationException($"Failed to obtain token: {responseContent}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error obtaining server-to-server token for client: {ClientId}", clientId);
            throw;
        }
    }

    public async Task<bool> ValidateTokenAsync(string token)
    {
        try
        {
            var validateEndpoint = $"{_baseUrl.TrimEnd('/')}/oauth/validate";
            
            _httpClient.DefaultRequestHeaders.Authorization = 
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var response = await _httpClient.PostAsync(validateEndpoint, null);
            var responseContent = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                var validationResult = JsonSerializer.Deserialize<JsonElement>(responseContent);
                return validationResult.GetProperty("isValid").GetBoolean();
            }

            _logger.LogWarning("Token validation failed. Response: {Response}", responseContent);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating server-to-server token");
            return false;
        }
    }
}