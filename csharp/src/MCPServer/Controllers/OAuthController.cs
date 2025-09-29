using MCPServer.Models;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json.Serialization;

namespace MCPServer.Controllers;

[ApiController]
[Route("oauth")]
public class OAuthController : ControllerBase
{
    private readonly ILogger<OAuthController> _logger;

    // OAuth client credentials store (in production, use proper client management)
    private readonly Dictionary<string, OAuthClient> _clients = new()
    {
        { "coordinator-agent-client", new OAuthClient 
            { 
                ClientId = "coordinator-agent-client", 
                ClientSecret = "coordinator-secret-key-2024", 
                AllowedScopes = ["mcp:tools", "mcp:tools:coordinator"] 
            }
        },
        { "customer-agent-client", new OAuthClient 
            { 
                ClientId = "customer-agent-client", 
                ClientSecret = "customer-secret-key-2024", 
                AllowedScopes = ["mcp:tools", "mcp:tools:customer"] 
            }
        },
        { "sales-agent-client", new OAuthClient 
            { 
                ClientId = "sales-agent-client", 
                ClientSecret = "sales-secret-key-2024", 
                AllowedScopes = ["mcp:tools", "mcp:tools:sales"] 
            }
        },
        { "transactions-agent-client", new OAuthClient 
            { 
                ClientId = "transactions-agent-client", 
                ClientSecret = "transactions-secret-key-2024", 
                AllowedScopes = ["mcp:tools", "mcp:tools:transactions"] 
            }
        }
    };

    public OAuthController(ILogger<OAuthController> logger)
    {
        _logger = logger;
    }

    [HttpPost("token")]
    public async Task<IActionResult> Token([FromForm] OAuthTokenRequest request)
    {
        try
        {
            _logger.LogInformation("OAuth token request received for client: {ClientId}", request.client_id);

            // Validate grant type
            if (request.grant_type != "client_credentials")
            {
                return BadRequest(new OAuthErrorResponse
                {
                    Error = "unsupported_grant_type",
                    ErrorDescription = "Only client_credentials grant type is supported"
                });
            }

            // Validate client credentials
            if (!_clients.TryGetValue(request.client_id, out var client) || 
                client.ClientSecret != request.client_secret)
            {
                _logger.LogWarning("Invalid client credentials for client: {ClientId}", request.client_id);
                return Unauthorized(new OAuthErrorResponse
                {
                    Error = "invalid_client",
                    ErrorDescription = "Invalid client credentials"
                });
            }

            // Validate requested scope
            var requestedScopes = string.IsNullOrEmpty(request.scope) 
                ? new[] { "mcp:tools" } 
                : request.scope.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            var invalidScopes = requestedScopes.Except(client.AllowedScopes).ToArray();
            if (invalidScopes.Any())
            {
                return BadRequest(new OAuthErrorResponse
                {
                    Error = "invalid_scope",
                    ErrorDescription = $"Invalid scopes: {string.Join(", ", invalidScopes)}"
                });
            }

            // Generate access token (simple implementation - in production use proper JWT/token service)
            var accessToken = GenerateAccessToken(client.ClientId, requestedScopes);
            var expiresIn = 3600; // 1 hour

            _logger.LogInformation("OAuth token issued for client {ClientId} with scopes: {Scopes}", 
                client.ClientId, string.Join(", ", requestedScopes));

            var tokenResponse = new OAuthTokenResponse
            {
                AccessToken = accessToken,
                TokenType = "Bearer",
                ExpiresIn = expiresIn,
                Scope = string.Join(" ", requestedScopes)
            };

            return Ok(tokenResponse);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing OAuth token request");
            return StatusCode(500, new OAuthErrorResponse
            {
                Error = "server_error",
                ErrorDescription = "Internal server error"
            });
        }
    }

    [HttpGet("clients")]
    public IActionResult GetClients()
    {
        // For development/testing purposes - return client info (without secrets)
        var clientInfo = _clients.Values.Select(c => new
        {
            clientId = c.ClientId,
            allowedScopes = c.AllowedScopes
        }).ToArray();

        return Ok(new { clients = clientInfo });
    }

    private string GenerateAccessToken(string clientId, string[] scopes)
    {
        // Simple token generation - in production, use proper JWT library or OAuth server
        var tokenData = new
        {
            client_id = clientId,
            scopes = scopes,
            issued_at = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            expires_at = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds()
        };

        var tokenJson = System.Text.Json.JsonSerializer.Serialize(tokenData);
        return Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(tokenJson));
    }

    [HttpPost("validate")]
    public IActionResult ValidateToken([FromHeader(Name = "Authorization")] string? authHeader)
    {
        try
        {
            if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
            {
                return BadRequest(new { message = "Invalid authorization header" });
            }

            var token = authHeader.Substring("Bearer ".Length);
            var isValid = ValidateAccessToken(token, out var clientId, out var scopes);

            if (!isValid)
            {
                return Unauthorized(new { message = "Invalid or expired token" });
            }

            return Ok(new { isValid = true, clientId, scopes });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating token");
            return StatusCode(500, new { message = "Internal server error" });
        }
    }

    private bool ValidateAccessToken(string token, out string? clientId, out string[]? scopes)
    {
        clientId = null;
        scopes = null;

        try
        {
            var tokenBytes = Convert.FromBase64String(token);
            var tokenJson = System.Text.Encoding.UTF8.GetString(tokenBytes);
            var tokenData = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(tokenJson);

            clientId = tokenData.GetProperty("client_id").GetString();
            var expiresAt = tokenData.GetProperty("expires_at").GetInt64();
            scopes = tokenData.GetProperty("scopes").EnumerateArray()
                .Select(s => s.GetString() ?? "").ToArray();

            // Check if token is expired
            if (DateTimeOffset.UtcNow.ToUnixTimeSeconds() > expiresAt)
            {
                return false;
            }

            return !string.IsNullOrEmpty(clientId);
        }
        catch
        {
            return false;
        }
    }
}