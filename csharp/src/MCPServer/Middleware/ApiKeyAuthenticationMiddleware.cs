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
            
            // Skip authentication for health checks and info endpoints
            if (context.Request.Path.StartsWithSegments("/health") || 
                context.Request.Path.StartsWithSegments("/mcp/info"))
            {
                _logger.LogDebug("📋 DEBUG: Skipping authentication for public endpoint: {Path}", requestPath);
                await _next(context);
                return;
            }

            // For MCP protocol, the root path "/" is where MCP requests are handled
            // We need to authenticate all POST requests to root and any /mcp paths
            var shouldAuthenticate = (context.Request.Path == "/" && requestMethod == "POST") || 
                                   context.Request.Path.StartsWithSegments("/mcp");

            if (!shouldAuthenticate)
            {
                _logger.LogDebug("📋 DEBUG: Skipping authentication for non-MCP endpoint: {Path}", requestPath);
                await _next(context);
                return;
            }

            _logger.LogInformation("🔐 DEBUG: Processing MCP endpoint authentication for: {Path}", requestPath);

            var apiKey = _configuration["McpServer:ApiKey"];
            var enforceAuth = _configuration.GetValue<bool>("McpServer:EnforceAuthentication", true); // Default to true for security

            _logger.LogInformation("🔧 DEBUG: Auth config - ApiKey configured: {HasApiKey}, EnforceAuth: {EnforceAuth}", 
                !string.IsNullOrEmpty(apiKey), enforceAuth);

            // If no API key is configured, log warning but continue (for development)
            if (string.IsNullOrEmpty(apiKey))
            {
                _logger.LogWarning("⚠️ WARNING: No API key configured. MCP server is running without authentication.");
                await _next(context);
                return;
            }

            // If authentication is not enforced, log and continue
            if (!enforceAuth)
            {
                _logger.LogInformation("ℹ️ DEBUG: Authentication is configured but not enforced. Set McpServer:EnforceAuthentication to true to enable.");
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
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsync("{\"error\": \"API Key is required\", \"code\": \"MISSING_API_KEY\"}");
                return;
            }

            // Validate API key
            var extractedApiKeyValue = extractedApiKey.ToString();
            if (!apiKey.Equals(extractedApiKeyValue))
            {
                _logger.LogWarning("❌ DEBUG: Invalid API key provided. Expected: {ExpectedKey}, Received: {ReceivedKey}", 
                    apiKey, extractedApiKeyValue);
                _logger.LogWarning("❌ DEBUG: Key comparison - Expected length: {ExpectedLength}, Received length: {ReceivedLength}", 
                    apiKey.Length, extractedApiKeyValue.Length);
                context.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsync("{\"error\": \"Invalid API Key\", \"code\": \"INVALID_API_KEY\"}");
                return;
            }

            // Check for supported transport types (streamable-http should be supported)
            var contentType = context.Request.ContentType;
            var acceptHeader = context.Request.Headers.Accept.ToString();
            
            _logger.LogDebug("📋 DEBUG: Content-Type: {ContentType}, Accept: {Accept}", contentType, acceptHeader);
            
            // Support for streamable-http and standard MCP requests
            if (!string.IsNullOrEmpty(contentType))
            {
                if (contentType.Contains("application/json") || 
                    contentType.Contains("text/plain") ||
                    contentType.Contains("application/vnd.mcp"))
                {
                    _logger.LogDebug("✅ DEBUG: Supported content type detected: {ContentType}", contentType);
                }
                else
                {
                    _logger.LogDebug("⚠️ DEBUG: Unknown content type, but proceeding: {ContentType}", contentType);
                }
            }

            _logger.LogInformation("✅ DEBUG: API key authentication successful");
            await _next(context);
        }
    }
}
