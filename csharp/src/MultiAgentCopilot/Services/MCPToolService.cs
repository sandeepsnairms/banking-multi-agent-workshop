using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MultiAgentCopilot.Models;
using MultiAgentCopilot.Models.Configuration;
using MultiAgentCopilot.MultiAgentCopilot.Models.Configuration;
using MultiAgentCopilot.Services;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace MultiAgentCopilot.MultiAgentCopilot.Services
{
    public class MCPToolService : IDisposable
    {
        private readonly ILogger<MCPToolService> _logger;
        private readonly MCPSettings _mcpSettings;
        private readonly Dictionary<AgentType, HttpClient> _httpClients;
        private readonly ILoggerFactory _loggerFactory;

        public MCPToolService(IOptions<MCPSettings> mcpOptions, ILogger<MCPToolService> logger, ILoggerFactory loggerFactory)
        {
            _mcpSettings = mcpOptions.Value ?? throw new ArgumentNullException(nameof(mcpOptions));
            _logger = logger;
            _loggerFactory = loggerFactory;
            _httpClients = new Dictionary<AgentType, HttpClient>();
            
            _logger.LogInformation("Initialized MCPToolService with settings: ConnectionType={ConnectionType}", _mcpSettings.ConnectionType);
        }

        private AgentConfiguration GetAgentConfiguration(AgentType agentType)
        {
            var agentName = agentType.ToString();

            // Get URL for the agent
            var url = _mcpSettings.GetType().GetProperty($"{agentName}EndpointUrl")?.GetValue(_mcpSettings, null) as string;

            if (string.IsNullOrEmpty(url))
            {
                throw new InvalidOperationException($"MCP endpoint URL for agent {agentName} is not configured.");
            }
            
            // Get tags for the agent (comma-separated)
            var tagsValue = _mcpSettings.GetType().GetProperty($"{agentName}ToolTags")?.GetValue(_mcpSettings, null) as string;

            if (string.IsNullOrEmpty(tagsValue))
            {
               _logger.LogDebug( $"MCP tool tags for agent {agentName} are not configured.");
            }

            var tags = new List<string>();

            if (!string.IsNullOrEmpty(tagsValue))
            {
                tags = tagsValue.Split(',', StringSplitOptions.RemoveEmptyEntries)
                               .Select(tag => tag.Trim())
                               .Where(tag => !string.IsNullOrEmpty(tag))
                               .ToList();
            }

            var config = new AgentConfiguration
            {
                Url = url,
                Tags = tags
            };

            _logger.LogInformation("Loading configuration for agent {AgentType}: URL={Url}, Tags=[{Tags}]",
                agentType, config.Url, string.Join(", ", config.Tags));

            return config;
        }

        private async Task<HttpClient> GetOrCreateHttpClientAsync(AgentType agentType)
        {
            // Check if we already have a client for this agent type
            if (_httpClients.TryGetValue(agentType, out var existingClient))
            {
                return existingClient;
            }

            try
            {
                var agentConfig = GetAgentConfiguration(agentType);
                var httpClient = new HttpClient
                {
                    BaseAddress = new Uri(agentConfig.Url),
                    Timeout = TimeSpan.FromSeconds(30)
                };

                // Add OAuth authentication if configured
                if (_mcpSettings.ConnectionType == MCPConnectionType.HTTP)
                {
                    var (clientId, clientSecret, scope) = GetAgentOAuthCredentials(agentType);
                    
                    _logger.LogInformation("Setting up OAuth authentication for MCP server at {Url} with client {ClientId}", 
                        agentConfig.Url, clientId);

                    // Get OAuth token
                    var token = await GetOAuthTokenAsync(agentConfig.Url, clientId, clientSecret, scope);
                    if (!string.IsNullOrEmpty(token))
                    {
                        httpClient.DefaultRequestHeaders.Authorization = 
                            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                    }
                }

                // Add common request headers (but not Content-Type - that's a content header)
                httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
                httpClient.DefaultRequestHeaders.Add("User-Agent", "MultiAgentCopilot-MCP-Client/1.0");

                _httpClients[agentType] = httpClient;
                _logger.LogInformation("Created HTTP client for agent {AgentType}", agentType);
                
                return httpClient;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create HTTP client for agent {AgentType}", agentType);
                throw;
            }
        }

        private async Task<string?> GetOAuthTokenAsync(string baseUrl, string clientId, string clientSecret, string scope)
        {
            try
            {
                using var oauthClient = new HttpClient();
                var tokenRequest = new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("grant_type", "client_credentials"),
                    new KeyValuePair<string, string>("client_id", clientId),
                    new KeyValuePair<string, string>("client_secret", clientSecret),
                    new KeyValuePair<string, string>("scope", scope)
                });

                var tokenUrl = new Uri(new Uri(baseUrl), "/oauth/token").ToString();
                var response = await oauthClient.PostAsync(tokenUrl, tokenRequest);
                
                if (response.IsSuccessStatusCode)
                {
                    var tokenResponse = await response.Content.ReadAsStringAsync();
                    var tokenData = JsonSerializer.Deserialize<JsonElement>(tokenResponse);
                    
                    if (tokenData.TryGetProperty("access_token", out var tokenElement))
                    {
                        return tokenElement.GetString();
                    }
                }
                
                _logger.LogWarning("Failed to get OAuth token. Status: {StatusCode}", response.StatusCode);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting OAuth token");
                return null;
            }
        }

        public async Task<IList<McpTool>> GetMcpTools(AgentType agent)
        {
            _logger.LogInformation("Retrieving MCP tools for agent: {AgentType}", agent);

            try
            {
                var httpClient = await GetOrCreateHttpClientAsync(agent);
                
                // Create content with proper Content-Type header
                var content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json");
                
                // Call the REST API endpoint
                var response = await httpClient.PostAsync("/mcp/tools/list", content);
                
                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError("Failed to get MCP tools for agent {AgentType}. Status: {StatusCode}, Error: {ErrorContent}", 
                        agent, response.StatusCode, errorContent);
                    return new List<McpTool>();
                }

                var responseContent = await response.Content.ReadAsStringAsync();
                var toolsResponse = JsonSerializer.Deserialize<JsonElement>(responseContent);

                var tools = new List<McpTool>();
                
                if (toolsResponse.TryGetProperty("tools", out var toolsArray))
                {
                    foreach (var toolElement in toolsArray.EnumerateArray())
                    {
                        var tool = new McpTool
                        {
                            Name = toolElement.GetProperty("name").GetString() ?? "",
                            Description = toolElement.GetProperty("description").GetString() ?? "",
                            InputSchema = toolElement.TryGetProperty("inputSchema", out var schemaElement) ? schemaElement : new JsonElement()
                        };

                        // Extract tags if available
                        if (toolElement.TryGetProperty("McpToolTags", out var tagsElement))
                        {
                            tool.Tags = tagsElement.GetString()?.Split(',', StringSplitOptions.RemoveEmptyEntries)
                                       .Select(t => t.Trim()).ToList() ?? new List<string>();
                        }

                        tools.Add(tool);
                    }
                }
                
                // Filter tools based on agent tags if configured
                var agentConfig = GetAgentConfiguration(agent);
                var filteredTools = FilterToolsByAgentTags(tools, agentConfig.Tags);

                _logger.LogInformation("Retrieved {TotalTools} tools, filtered to {FilteredTools} for agent {AgentType} using tags [{Tags}]",
                    tools.Count, filteredTools.Count, agent, string.Join(", ", agentConfig.Tags));

                return filteredTools;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve MCP tools for agent {AgentType}", agent);
                return new List<McpTool>();
            }
        }

        /// <summary>
        /// Execute a tool via HTTP REST API
        /// </summary>
        public async Task<object?> CallToolAsync(AgentType agent, string toolName, Dictionary<string, object>? arguments = null)
        {
            try
            {
                var httpClient = await GetOrCreateHttpClientAsync(agent);
                
                _logger.LogInformation("Executing MCP tool {ToolName} for agent {AgentType}", toolName, agent);

                var request = new
                {
                    Name = toolName,
                    Arguments = arguments ?? new Dictionary<string, object>()
                };

                var json = JsonSerializer.Serialize(request);
                var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
                
                var response = await httpClient.PostAsync("/mcp/tools/call", content);
                
                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError("Failed to call tool {ToolName} for agent {AgentType}. Status: {StatusCode}, Error: {ErrorContent}", 
                        toolName, agent, response.StatusCode, errorContent);
                    return null;
                }

                var responseContent = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<JsonElement>(responseContent);
                
                // Extract content from the response
                if (result.TryGetProperty("content", out var contentArray))
                {
                    var firstContent = contentArray.EnumerateArray().FirstOrDefault();
                    if (firstContent.TryGetProperty("text", out var textElement))
                    {
                        return textElement.GetString();
                    }
                }
                
                return responseContent;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calling tool {ToolName} for agent {AgentType}", toolName, agent);
                return null;
            }
        }

        /// <summary>
        /// Gets agent-specific OAuth credentials from configuration
        /// </summary>
        private (string clientId, string clientSecret, string scope) GetAgentOAuthCredentials(AgentType agentType)
        {
            // Note: There's a typo in MCPSettings - "Cordinator" instead of "Coordinator"
            // We'll handle both the typo and the correct spelling
            var agentName = agentType switch
            {
                AgentType.Coordinator => "Cordinator", // Using the typo from MCPSettings
                AgentType.CustomerSupport => "Customer",
                AgentType.Sales => "Sales",
                AgentType.Transactions => "Transactions",
                _ => agentType.ToString()
            };
            
            var clientId = _mcpSettings.GetType().GetProperty($"{agentName}ClientId")?.GetValue(_mcpSettings, null) as string 
                          ?? throw new InvalidOperationException($"OAuth Client ID for agent {agentName} is not configured.");
            
            var clientSecret = _mcpSettings.GetType().GetProperty($"{agentName}ClientSecret")?.GetValue(_mcpSettings, null) as string
                              ?? throw new InvalidOperationException($"OAuth Client Secret for agent {agentName} is not configured.");
            
            var scope = _mcpSettings.GetType().GetProperty($"{agentName}Scope")?.GetValue(_mcpSettings, null) as string
                       ?? "mcp:tools"; // Default scope
            
            return (clientId, clientSecret, scope);
        }

        private IList<McpTool> FilterToolsByAgentTags(
            IList<McpTool> allTools,
            List<string> agentTags)
        {
            // If agent has no tags configured, return all tools
            if (agentTags == null || agentTags.Count == 0)
            {
                _logger.LogDebug("Agent has no tags configured, returning all {ToolCount} tools", allTools.Count);
                return allTools;
            }

            var filteredTools = new List<McpTool>();

            foreach (var tool in allTools)
            {
                // Check if tool has matching tags
                if (HasMatchingTags(tool, agentTags))
                {
                    filteredTools.Add(tool);
                }
            }

            // Log if no tools match the agent's tags
            if (filteredTools.Count == 0 && allTools.Count > 0)
            {
                _logger.LogWarning("No tools found matching agent tags. Agent tags: [{AgentTags}], Available tools: [{ToolNames}]",
                    string.Join(", ", agentTags),
                    string.Join(", ", allTools.Select(t => t.Name)));
            }

            return filteredTools;
        }

        private bool HasMatchingTags(McpTool tool, List<string> agentTags)
        {
            _logger.LogDebug("Checking tags for tool {ToolName}", tool.Name);

            var toolTags = tool.Tags ?? new List<string>();

            _logger.LogDebug("Tool {ToolName} has tags: [{ToolTags}]", tool.Name, string.Join(", ", toolTags));

            // If no tags found in tool, return true (allow all tools without tags)
            if (toolTags.Count == 0)
            {
                _logger.LogDebug("Tool {ToolName} has no tags defined, allowing access", tool.Name);
                return true;
            }

            // Check if any of the tool's tags match any of the agent's tags
            foreach (var agentTag in agentTags)
            {
                foreach (var toolTag in toolTags)
                {
                    if (string.Equals(toolTag, agentTag, StringComparison.OrdinalIgnoreCase))
                    {
                        _logger.LogDebug("Found matching tag '{Tag}' between tool {ToolName} and agent tags", toolTag, tool.Name);
                        return true;
                    }
                }
            }

            // Also check for universal tags
            foreach (var toolTag in toolTags)
            {
                if (string.Equals(toolTag, "*", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogDebug("Found universal tag '{Tag}' for tool {ToolName}", toolTag, tool.Name);
                    return true;
                }
            }

            _logger.LogDebug("No matching tags found between tool {ToolName} tags [{ToolTags}] and agent tags [{AgentTags}]", 
                tool.Name, string.Join(", ", toolTags), string.Join(", ", agentTags));
            
            return false;
        }

        public void Dispose()
        {
            // Dispose all HTTP clients
            foreach (var client in _httpClients.Values)
            {
                client?.Dispose();
            }
            _httpClients.Clear();
        }
    }

    // Simple MCP tool model
    public class McpTool
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public JsonElement InputSchema { get; set; }
        public List<string> Tags { get; set; } = new List<string>();
    }

    // Configuration for each agent
    public class AgentConfiguration
    {
        public string Url { get; set; } = string.Empty;
        public List<string> Tags { get; set; } = new List<string>();
    }
}