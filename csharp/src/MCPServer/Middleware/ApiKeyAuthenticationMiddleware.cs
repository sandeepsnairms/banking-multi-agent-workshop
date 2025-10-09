using System.Net;

namespace MCPServer.Middleware
{
    public class ApiKeyAuthenticationMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly IConfiguration _configuration;
        private readonly ILogger<ApiKeyAuthenticationMiddleware> _logger;

        public ApiKeyAuthenticationMiddleware(RequestDelegate next, IConfiguration configuration, ILogger<ApiKeyAuthenticationMiddleware> logger)
        {
            _next = next;
            _configuration = configuration;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var requestPath = context.Request.Path.Value;
            var requestMethod = context.Request.Method;
            
            _logger.LogInformation("🔍 DEBUG: Authentication middleware processing request: {Method} {Path}", requestMethod, requestPath);
            
            // Skip authentication for health checks and other non-MCP endpoints
            if (!context.Request.Path.StartsWithSegments("/mcp"))
            {
                _logger.LogDebug("📋 DEBUG: Skipping authentication for non-MCP endpoint: {Path}", requestPath);
                await _next(context);
                return;
            }

            _logger.LogInformation("🔐 DEBUG: Processing MCP endpoint authentication for: {Path}", requestPath);

            var apiKey = _configuration["McpServer:ApiKey"];
            var enforceAuth = _configuration.GetValue<bool>("McpServer:EnforceAuthentication", false);

            _logger.LogInformation("🔧 DEBUG: Auth config - ApiKey configured: {HasApiKey}, EnforceAuth: {EnforceAuth}", 
                !string.IsNullOrEmpty(apiKey), enforceAuth);

            // If no API key is configured or authentication is not enforced, skip authentication
            if (string.IsNullOrEmpty(apiKey) || !enforceAuth)
            {
                if (string.IsNullOrEmpty(apiKey))
                {
                    _logger.LogWarning("⚠️ DEBUG: No API key configured. MCP server is running without authentication.");
                }
                else
                {
                    _logger.LogInformation("ℹ️ DEBUG: Authentication is configured but not enforced. Set McpServer:EnforceAuthentication to true to enable.");
                }
                _logger.LogInformation("✅ DEBUG: Proceeding without authentication check");
                await _next(context);
                return;
            }

            // Log all headers for debugging
            _logger.LogDebug("📋 DEBUG: Request headers:");
            foreach (var header in context.Request.Headers)
            {
                var headerValue = header.Key.Contains("Key", StringComparison.OrdinalIgnoreCase) ? "***REDACTED***" : string.Join(", ", header.Value.ToArray());
                _logger.LogDebug("  {HeaderName}: {HeaderValue}", header.Key, headerValue);
            }

            // Check for API key in headers
            if (!context.Request.Headers.TryGetValue("X-MCP-API-Key", out var extractedApiKey))
            {
                _logger.LogWarning("❌ DEBUG: API key missing from request headers");
                context.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
                await context.Response.WriteAsync("API Key is required");
                return;
            }

            if (!apiKey.Equals(extractedApiKey))
            {
                _logger.LogWarning("❌ DEBUG: Invalid API key provided. Expected length: {ExpectedLength}, Received length: {ReceivedLength}", 
                    apiKey.Length, extractedApiKey.ToString().Length);
                context.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
                await context.Response.WriteAsync("Invalid API Key");
                return;
            }

            _logger.LogInformation("✅ DEBUG: API key authentication successful");
            await _next(context);
        }
    }
}
