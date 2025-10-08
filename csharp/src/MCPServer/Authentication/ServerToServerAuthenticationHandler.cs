using MCPServer.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace MCPServer.Authentication;

public class ServerToServerAuthenticationOptions : AuthenticationSchemeOptions
{
    public const string DefaultScheme = "ServerToServer";
    public string Scheme => DefaultScheme;
}

public class ServerToServerAuthenticationHandler : AuthenticationHandler<ServerToServerAuthenticationOptions>
{
    private readonly OAuthSettings _oauthSettings;
    private readonly ILogger<ServerToServerAuthenticationHandler> _logger;

    public ServerToServerAuthenticationHandler(
        IOptionsMonitor<ServerToServerAuthenticationOptions> options,
        ILoggerFactory loggerFactory,
        UrlEncoder encoder,
        IOptions<OAuthSettings> oauthSettings)
        : base(options, loggerFactory, encoder)
    {
        _oauthSettings = oauthSettings.Value;
        _logger = loggerFactory.CreateLogger<ServerToServerAuthenticationHandler>();
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        // Check for Authorization header
        if (!Request.Headers.ContainsKey("Authorization"))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var authHeader = Request.Headers["Authorization"].ToString();
        if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
        {
            return Task.FromResult(AuthenticateResult.Fail("Invalid authorization header format"));
        }

        var token = authHeader.Substring("Bearer ".Length);

        try
        {
            if (ValidateServerToServerToken(token, out var clientId, out var scopes))
            {
                var claims = new List<Claim>
                {
                    new(ClaimTypes.Name, clientId),
                    new(ClaimTypes.NameIdentifier, clientId),
                    new("client_id", clientId),
                    new("auth_type", "server_to_server")
                };

                // Add scope claims
                foreach (var scope in scopes)
                {
                    claims.Add(new Claim("scope", scope));
                }

                var identity = new ClaimsIdentity(claims, Scheme.Name);
                var principal = new ClaimsPrincipal(identity);
                var ticket = new AuthenticationTicket(principal, Scheme.Name);

                _logger.LogInformation("Server-to-server authentication successful for client: {ClientId}", clientId);
                return Task.FromResult(AuthenticateResult.Success(ticket));
            }
            else
            {
                _logger.LogWarning("Invalid server-to-server token received");
                return Task.FromResult(AuthenticateResult.Fail("Invalid token"));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating server-to-server token");
            return Task.FromResult(AuthenticateResult.Fail("Token validation error"));
        }
    }

    private bool ValidateServerToServerToken(string token, out string clientId, out string[] scopes)
    {
        clientId = string.Empty;
        scopes = Array.Empty<string>();

        try
        {
            var tokenBytes = Convert.FromBase64String(token);
            var tokenJson = System.Text.Encoding.UTF8.GetString(tokenBytes);
            var tokenData = JsonSerializer.Deserialize<JsonElement>(tokenJson);

            var extractedClientId = tokenData.GetProperty("client_id").GetString() ?? string.Empty;
            var expiresAt = tokenData.GetProperty("expires_at").GetInt64();
            scopes = tokenData.GetProperty("scopes").EnumerateArray()
                .Select(s => s.GetString() ?? string.Empty)
                .Where(s => !string.IsNullOrEmpty(s))
                .ToArray();

            // Check if token is expired
            if (DateTimeOffset.UtcNow.ToUnixTimeSeconds() > expiresAt)
            {
                _logger.LogWarning("Token expired for client: {ClientId}", extractedClientId);
                return false;
            }

            // Validate client exists in configuration
            var clientExists = _oauthSettings.Clients.Any(c => c.ClientId == extractedClientId);
            if (!clientExists)
            {
                _logger.LogWarning("Unknown client ID in token: {ClientId}", extractedClientId);
                return false;
            }

            // Set the out parameter
            clientId = extractedClientId;
            return !string.IsNullOrEmpty(extractedClientId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing server-to-server token");
            return false;
        }
    }
}