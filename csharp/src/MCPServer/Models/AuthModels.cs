using System.Text.Json.Serialization;

namespace MCPServer.Models;

// OAuth 2.0 form request model (for [FromForm] binding)
public class OAuthTokenRequest
{
    public string grant_type { get; set; } = string.Empty;
    public string client_id { get; set; } = string.Empty;
    public string client_secret { get; set; } = string.Empty;
    public string? scope { get; set; }
}

// OAuth 2.0 JSON response models
public record OAuthTokenResponse
{
    [JsonPropertyName("access_token")]
    public string AccessToken { get; init; } = string.Empty;
    
    [JsonPropertyName("token_type")]
    public string TokenType { get; init; } = "Bearer";
    
    [JsonPropertyName("expires_in")]
    public int ExpiresIn { get; init; }
    
    [JsonPropertyName("scope")]
    public string? Scope { get; init; }
}

public record OAuthErrorResponse
{
    [JsonPropertyName("error")]
    public string Error { get; init; } = string.Empty;
    
    [JsonPropertyName("error_description")]
    public string? ErrorDescription { get; init; }
}

public record OAuthClient
{
    public string ClientId { get; init; } = string.Empty;
    public string ClientSecret { get; init; } = string.Empty;
    public string[] AllowedScopes { get; init; } = Array.Empty<string>();
}