using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Client;
using MultiAgentCopilot.Factories;
using MultiAgentCopilot.Models;
using MultiAgentCopilot.Models.Configuration;
using MultiAgentCopilot.Transport;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;

namespace MultiAgentCopilot.MultiAgentCopilot.Services
{
    public class MCPToolService : IAsyncDisposable, IDisposable
    {
        private readonly ILogger<MCPToolService> _logger;
        private readonly MCPSettings _mcpSettings;
        private readonly ILoggerFactory _loggerFactory;


        public MCPToolService(IOptions<MCPSettings> mcpOptions, ILogger<MCPToolService> logger, ILoggerFactory loggerFactory)
        {
            _mcpSettings = mcpOptions.Value ?? throw new ArgumentNullException(nameof(mcpOptions));
            _logger = logger;
            _loggerFactory = loggerFactory;

            // Validate configuration
            ValidateConfiguration();
        }

        private void ValidateConfiguration()
        {
            if (_mcpSettings.Servers == null || !_mcpSettings.Servers.Any())
            {
                _logger.LogWarning("No MCP servers configured in MCPSettings.Servers");
                return;
            }

            foreach (var server in _mcpSettings.Servers)
            {
                if (string.IsNullOrWhiteSpace(server.AgentName))
                {
                    _logger.LogWarning("MCP server configuration has empty AgentName");
                }

                if (string.IsNullOrWhiteSpace(server.Url))
                {
                    _logger.LogWarning("MCP server '{AgentName}' has empty Url", server.AgentName);
                }

                if (string.IsNullOrWhiteSpace(server.Key))
                {
                    _logger.LogWarning("MCP server '{AgentName}' has empty API Key", server.AgentName);
                }

                // Validate URL format
                if (!string.IsNullOrWhiteSpace(server.Url) && !Uri.TryCreate(server.Url, UriKind.Absolute, out _))
                {
                    _logger.LogWarning("MCP server '{AgentName}' has invalid Url format: {Url}", server.AgentName, server.Url);
                }
            }

            _logger.LogInformation("MCP configuration validated for {ServerCount} servers", _mcpSettings.Servers.Count);
        }


        
        private MCPServerSettings GetMCPServerSettings(AgentType agentType)
        {
            return null;
           
        }
       

        /// <summary>
        /// Creates or retrieves a cached MCP client for the specified agent type
        /// </summary>
        private async Task<McpClient> CreateMcpClientAsync(AgentType agentType, MCPServerSettings settings)
        {
             return null;           

        }

        /// <summary>
        /// Configures the HTTP transport for streamable-http support
        /// </summary>
        private async Task ConfigureTransportForStreamableHttp(HttpClientTransport transport, MCPServerSettings settings)
        {
            try
            {
                _logger.LogDebug("Configuring transport for streamable-http support...");

                // Use reflection to access the underlying HttpClient
                var transportType = transport.GetType();
                var httpClientField = transportType.GetField("_httpClient", BindingFlags.NonPublic | BindingFlags.Instance);
                
                if (httpClientField?.GetValue(transport) is HttpClient httpClient)
                {
                    // Ensure proper headers for streamable-http
                    if (!httpClient.DefaultRequestHeaders.Accept.Any(h => h.MediaType == "application/json"))
                    {
                        httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                    }

                    // Support for MCP content types
                    if (!httpClient.DefaultRequestHeaders.Accept.Any(h => h.MediaType == "application/vnd.mcp"))
                    {
                        httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.mcp"));
                    }

                    // Verify API key header is present
                    if (!httpClient.DefaultRequestHeaders.Contains("X-MCP-API-Key"))
                    {
                        _logger.LogWarning("X-MCP-API-Key header not found, adding manually...");
                        httpClient.DefaultRequestHeaders.Add("X-MCP-API-Key", settings.Key);
                    }

                    // Set appropriate timeout for MCP operations
                    if (httpClient.Timeout == System.Threading.Timeout.InfiniteTimeSpan)
                    {
                        httpClient.Timeout = TimeSpan.FromSeconds(30);
                    }

                    _logger.LogDebug("Transport configured for streamable-http");
                }
                else
                {
                    _logger.LogWarning("Could not access HttpClient to configure streamable-http support");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to configure transport for streamable-http: {Message}", ex.Message);
                // Non-fatal error, continue with default configuration
            }

            await Task.CompletedTask;
        }

        //TO DO: ADD GetMcpTools

        public async Task<IList<McpClientTool>> GetMcpTools(AgentType agent)
        {
             return null;
        }

        /// <summary>
        ///  Filter MCP tools  by tags from the tool's description or metadata
        /// </summary>
        public IList<McpClientTool> FilterToolsByTags(IList<McpClientTool> allTools, params string[] tags)
        {
             if (!tags.Any())
                return allTools;

            // Filter tools by tags - now parse embedded tags from description and exclude tag pattern from description search
            var filteredTools = allTools.Where(tool =>
            {
                var toolTags = ExtractTagsFromDescription(tool.Description);                
                return tags.Any(tag =>
                    tool.Description.Contains(tag, StringComparison.OrdinalIgnoreCase));
            }).ToList();


            _logger.LogInformation("Filtered to {FilteredCount} tools from {TotalCount} total tools", 
                filteredTools.Count, allTools.Count);

            return filteredTools;
        }
                
        /// <summary>
        /// Extracts tags from tool description that are embedded in [TAGS: ...] format
        /// </summary>
        private List<string> ExtractTagsFromDescription(string description)
        {
            var tags = new List<string>();
            
            if (string.IsNullOrEmpty(description))
                return tags;

            // Look for [TAGS: tag1,tag2,tag3] pattern
            var tagPattern = @"\[TAGS:\s*([^\]]+)\]";
            var match = System.Text.RegularExpressions.Regex.Match(description, tagPattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            
            if (match.Success)
            {
                var tagString = match.Groups[1].Value;
                tags.AddRange(tagString.Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(tag => tag.Trim())
                    .Where(tag => !tag.StartsWith("priority:", StringComparison.OrdinalIgnoreCase)));
            }

            return tags;
        }

       

        public async ValueTask DisposeAsync()
        {
            _logger.LogInformation("Disposing MCPToolService...");

        }

        public void Dispose()
        {
            // Synchronous dispose for compatibility
            try
            {
                DisposeAsync().AsTask().GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error during synchronous dispose: {Message}", ex.Message);
            }
            
            GC.SuppressFinalize(this);
        }
    }
}