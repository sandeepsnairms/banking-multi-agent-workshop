using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Identity.Client;
using ModelContextProtocol;
using ModelContextProtocol.Authentication;
using ModelContextProtocol.Client;
using MultiAgentCopilot.Models;
using MultiAgentCopilot.Models.Configuration;
using MultiAgentCopilot.MultiAgentCopilot.Models.Configuration;
using MultiAgentCopilot.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;
using MultiAgentCopilot.Services.MCP;

namespace MultiAgentCopilot.MultiAgentCopilot.Services
{
    public class MCPToolService : IAsyncDisposable
    {
        private readonly ILogger<MCPToolService> _logger;
        private readonly MCPSettings _mcpSettings;
        private readonly Dictionary<AgentType, McpOAuthClient> _mcpClients;
        private readonly ILoggerFactory _loggerFactory;

        public MCPToolService(IOptions<MCPSettings> mcpOptions, ILogger<MCPToolService> logger, ILoggerFactory loggerFactory)
        {
            _mcpSettings = mcpOptions.Value ?? throw new ArgumentNullException(nameof(mcpOptions));
            _logger = logger;
            _loggerFactory = loggerFactory;
            _mcpClients = new Dictionary<AgentType, McpOAuthClient>();
            
           
            // CRITICAL: Test MCP connectivity during initialization
            //Task.Run(async () => await TestMCPConnectivityAsync());
        }

        private MCPSettings.MCPServerSettings GetMCPServerSettings(AgentType agentType)
        {
            // Map AgentType to the agent name used in MCPSettings
            var agentName = agentType switch
            {
                AgentType.Coordinator => "Coordinator", 
                AgentType.CustomerSupport => "CustomerSupport",
                AgentType.Sales => "Sales", 
                AgentType.Transactions => "Transactions",
                _ => agentType.ToString()
            };

            // Find the server configuration for this agent type
            var serverSettings = _mcpSettings.Servers?.FirstOrDefault(s => 
                string.Equals(s.AgentName, agentName, StringComparison.OrdinalIgnoreCase));

            if (serverSettings == null)
            {
                throw new InvalidOperationException($"MCP server configuration for agent '{agentName}' not found in MCPSettings.Servers.");
            }

            return serverSettings;
            
        }

        //private async Task<McpOAuthClient> GetOrCreateMcpClientAsync(AgentType agentType)
        //{
        //    _logger.LogInformation("🌐 GetOrCreateMcpClientAsync called for agent: {AgentType}", agentType);
            
        //    // Check if we already have a client for this agent type
        //    if (_mcpClients.TryGetValue(agentType, out var existingClient))
        //    {
        //        _logger.LogInformation("♻️ Reusing existing MCP client for agent {AgentType}", agentType);
        //        return existingClient;
        //    }

        //    try
        //    {
        //        var agentConfig = GetAgentConfiguration(agentType);
        //        _logger.LogInformation("🔧 Creating new MCP OAuth client for agent {AgentType} at URL: {Url}", agentType, agentConfig.Url);
                
        //        // Create MCP OAuth client with proper authentication
        //        var mcpClient = new McpOAuthClient(
        //            authority: "https://login.microsoftonline.com/common", // Default Azure AD authority
        //            clientId: agentConfig.ClientId,
        //            clientSecret: agentConfig.ClientSecret,
        //            scope: agentConfig.Scope,
        //            baseUrl: agentConfig.Url,
        //            logger: _logger
        //        );

        //        // Initialize the client with OAuth authentication
        //        await mcpClient.InitializeAsync();

        //        _mcpClients[agentType] = mcpClient;
        //        _logger.LogInformation("✅ Created and cached MCP OAuth client for agent {AgentType}", agentType);
                
        //        return mcpClient;
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogError(ex, "💥 Failed to create MCP OAuth client for agent {AgentType}: {ExceptionMessage}", agentType, ex.Message);
        //        throw;
        //    }
        //}

        public async Task<IList<AIFunction>> GetMcpTools(AgentType agent)
        {
            _logger.LogInformation("🔍 STARTING GetMcpTools for agent: {AgentType}", agent);

            try
            {
                // Get agent configuration
                var settings = GetMCPServerSettings(agent);
                
                // Validate required OAuth configuration
                if (settings.OAuth == null || 
                    string.IsNullOrEmpty(settings.OAuth.TokenEndpoint) || 
                    string.IsNullOrEmpty(settings.OAuth.ClientId) || 
                    string.IsNullOrEmpty(settings.OAuth.ClientSecret))
                {
                    _logger.LogError("❌ Incomplete OAuth configuration for agent {AgentType}. Missing OAuth settings", agent);
                    return new List<AIFunction>();
                }

                // Create an HttpClientFactory wrapper for dependency injection
                var httpClientFactory = new SimpleHttpClientFactory();
                
                // Create McpClientService with the required parameters
                var mcpClientService = new McpClientService(
                    httpClientFactory, 
                    _loggerFactory, 
                    settings);

                // Initialize the client
                var initialized = await mcpClientService.InitializeAsync();
                if (!initialized)
                {
                    _logger.LogError("❌ Failed to initialize MCP client for agent {AgentType}", agent);
                    return new List<AIFunction>();
                }

                // Get tools from the MCP server
                var tools = await mcpClientService.GetMcpClientToolsAsync();
                
                _logger.LogInformation("✅ COMPLETED GetMcpTools for {AgentType}: Retrieved {ToolCount} tools",
                    agent, tools.Count);

                return tools;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "💥 EXCEPTION in GetMcpTools for agent {AgentType}: {ExceptionMessage}", agent, ex.Message);
                return new List<AIFunction>();
            }
        }

        /// <summary>
        /// Execute a tool via official MCP client with OAuth authentication
        /// </summary>
        //public async Task<object?> CallToolAsync(AgentType agent, string toolName, Dictionary<string, object>? arguments = null)
        //{
        //    _logger.LogInformation("🚀 STARTING CallToolAsync: Agent={AgentType}, Tool={ToolName}, Arguments={Arguments}", 
        //        agent, toolName, arguments != null ? JsonSerializer.Serialize(arguments) : "null");

        //    try
        //    {
        //        // Get agent configuration
        //        var agentConfig = GetAgentConfiguration(agent);

        //        // Validate required OAuth configuration
        //        if (string.IsNullOrEmpty(agentConfig.TokenEndpoint) || 
        //            string.IsNullOrEmpty(agentConfig.ClientId) || 
        //            string.IsNullOrEmpty(agentConfig.ClientSecret))
        //        {
        //            _logger.LogError("❌ Incomplete OAuth configuration for agent {AgentType}. Missing TokenEndpoint, ClientId, or ClientSecret", agent);
        //            return $"Error: Incomplete OAuth configuration for agent {agent}";
        //        }

        //        // Acquire access token via client credentials flow
        //        using var httpClient = new HttpClient();
        //        var tokenRequest = new HttpRequestMessage(HttpMethod.Post, agentConfig.TokenEndpoint)
        //        {
        //            Content = new FormUrlEncodedContent(new Dictionary<string, string>
        //            {
        //                { "grant_type", "client_credentials" },
        //                { "client_id", agentConfig.ClientId },
        //                { "client_secret", agentConfig.ClientSecret },
        //                { "scope", agentConfig.Scope }
        //            })
        //        };

        //        _logger.LogInformation("🔐 Requesting OAuth token from {TokenEndpoint} for client {ClientId}", agentConfig.TokenEndpoint, agentConfig.ClientId);

        //        var tokenResponse = await httpClient.SendAsync(tokenRequest);
                
        //        if (!tokenResponse.IsSuccessStatusCode)
        //        {
        //            var errorContent = await tokenResponse.Content.ReadAsStringAsync();
        //            _logger.LogError("❌ Failed to acquire OAuth token. Status: {StatusCode}, Error: {ErrorContent}", tokenResponse.StatusCode, errorContent);
        //            return $"Error acquiring OAuth token: {errorContent}";
        //        }

        //        var jsonContent = await tokenResponse.Content.ReadAsStringAsync();
        //        var tokenData = JsonDocument.Parse(jsonContent);
        //        string accessToken = tokenData.RootElement.GetProperty("access_token").GetString() ?? "";

        //        if (string.IsNullOrEmpty(accessToken))
        //        {
        //            _logger.LogError("❌ Received empty access token from OAuth endpoint");
        //            return "Error: Received empty access token from OAuth endpoint";
        //        }

        //        _logger.LogInformation("✅ Successfully acquired OAuth token for agent {AgentType}", agent);

        //        // Set Bearer token on all requests to the MCP server
        //        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        //        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());

        //        // Create MCP client with HTTP client containing the access token header
        //        var transport = new HttpClientTransport(new()
        //        {
        //            Endpoint = new Uri(agentConfig.Url),
        //            Name = $"{agent} Banking Client"
        //        }, httpClient, loggerFactory);

        //        await using var mcpClient = await McpClient.CreateAsync(transport, loggerFactory: loggerFactory);
                
        //        _logger.LogInformation("🔧 Got MCP client for agent {AgentType}, calling tool {ToolName}", agent, toolName);

        //        // Use the MCP client to invoke tool
        //        var result = await mcpClient.CallToolAsync(toolName, arguments);
                
        //        _logger.LogInformation("📨 MCP Tool Result: Agent={AgentType}, Tool={ToolName}, ContentCount={ContentCount}", 
        //            agent, toolName, result.Content?.Count ?? 0);
                
        //        // Extract content from MCP tool result
        //        if (result.Content != null && result.Content.Count > 0)
        //        {
        //            // Get the first content block and try to extract text content
        //            var firstContent = result.Content.FirstOrDefault();
        //            if (firstContent != null)
        //            {
        //                // Try to get text content based on the actual structure of ContentBlock
        //                string? textResult = null;
                        
        //                // Check if it's a text content block type
        //                if (firstContent.Type == "text" && firstContent is { } contentBlock)
        //                {
        //                    // Access the text property - may need to use different property name
        //                    textResult = contentBlock.ToString(); // Fallback to ToString if direct text access fails
        //                }
                        
        //                if (!string.IsNullOrEmpty(textResult))
        //                {
        //                    _logger.LogInformation("✅ CallToolAsync SUCCESS: Agent={AgentType}, Tool={ToolName}, Result={Result}", 
        //                        agent, toolName, textResult);
        //                    return textResult;
        //                }
        //            }
        //        }
                
        //        // If no content, return success message
        //        var successMessage = "Tool executed successfully";
        //        _logger.LogInformation("✅ CallToolAsync SUCCESS (no content): Agent={AgentType}, Tool={ToolName}", 
        //            agent, toolName);
        //        return successMessage;
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogError(ex, "💥 CallToolAsync EXCEPTION: Agent={AgentType}, Tool={ToolName}, Exception={ExceptionMessage}", 
        //            agent, toolName, ex.Message);
        //        return $"Error executing {toolName}: {ex.Message}";
        //    }
        //}

        //private IList<McpTool> FilterToolsByAgentTags(
        //    IList<McpTool> allTools,
        //    List<string> agentTags)
        //{
        //    // If agent has no tags configured, return all tools
        //    if (agentTags == null || agentTags.Count == 0)
        //    {
        //        _logger.LogDebug("Agent has no tags configured, returning all {ToolCount} tools", allTools.Count);
        //        return allTools;
        //    }

        //    var filteredTools = new List<McpTool>();

        //    foreach (var tool in allTools)
        //    {
        //        // Check if tool has matching tags
        //        if (HasMatchingTags(tool, agentTags))
        //        {
        //            filteredTools.Add(tool);
        //        }
        //    }

        //    // Log if no tools match the agent's tags
        //    if (filteredTools.Count == 0 && allTools.Count > 0)
        //    {
        //        _logger.LogWarning("No tools found matching agent tags. Agent tags: [{AgentTags}], Available tools: [{ToolNames}]",
        //            string.Join(", ", agentTags),
        //            string.Join(", ", allTools.Select(t => t.Name)));
        //    }

        //    return filteredTools;
        //}

        //private bool HasMatchingTags(McpTool tool, List<string> agentTags)
        //{
        //    _logger.LogDebug("Checking tags for tool {ToolName}", tool.Name);

        //    var toolTags = tool.Tags ?? new List<string>();

        //    _logger.LogDebug("Tool {ToolName} has tags: [{ToolTags}]", tool.Name, string.Join(", ", toolTags));

        //    // If no tags found in tool, return true (allow all tools without tags)
        //    if (toolTags.Count == 0)
        //    {
        //        _logger.LogDebug("Tool {ToolName} has no tags defined, allowing access", tool.Name);
        //        return true;
        //    }

        //    // Check if any of the tool's tags match any of the agent's tags
        //    foreach (var agentTag in agentTags)
        //    {
        //        foreach (var toolTag in toolTags)
        //        {
        //            if (string.Equals(toolTag, agentTag, StringComparison.OrdinalIgnoreCase))
        //            {
        //                _logger.LogDebug("Found matching tag '{Tag}' between tool {ToolName} and agent tags", toolTag, tool.Name);
        //                return true;
        //            }
        //        }
        //    }

        //    // Also check for universal tags
        //    foreach (var toolTag in toolTags)
        //    {
        //        if (string.Equals(toolTag, "*", StringComparison.OrdinalIgnoreCase))
        //        {
        //            _logger.LogDebug("Found universal tag '{Tag}' for tool {ToolName}", toolTag, tool.Name);
        //            return true;
        //        }
        //    }

        //    _logger.LogDebug("No matching tags found between tool {ToolName} tags [{ToolTags}] and agent tags [{AgentTags}]", 
        //        tool.Name, string.Join(", ", toolTags), string.Join(", ", agentTags));
            
        //    return false;
        //}

        /// <summary>
        /// Test MCP connectivity during initialization
        /// </summary>
        //private async Task TestMCPConnectivityAsync()
        //{
        //    try
        //    {
        //        _logger.LogInformation("🧪 TESTING MCP CONNECTIVITY...");
                
        //        // Test each agent type
        //        var agentTypes = Enum.GetValues<AgentType>();
        //        var successCount = 0;
        //        var totalCount = agentTypes.Length;

        //        foreach (var agentType in agentTypes)
        //        {
        //            try
        //            {
        //                _logger.LogInformation("🧪 Testing connectivity for agent {AgentType}...", agentType);
        //                var tools = await GetMcpTools(agentType);
        //                _logger.LogInformation("✅ Agent {AgentType} connectivity test: SUCCESS - {ToolCount} tools available", 
        //                    agentType, tools.Count);
        //                successCount++;
        //            }
        //            catch (Exception ex)
        //            {
        //                _logger.LogError(ex, "❌ Agent {AgentType} connectivity test: FAILED - {ExceptionMessage}", 
        //                    agentType, ex.Message);
        //            }
        //        }

        //        if (successCount == totalCount)
        //        {
        //            _logger.LogInformation("🎉 MCP CONNECTIVITY TEST: ALL AGENTS CONNECTED ({SuccessCount}/{TotalCount})", 
        //                successCount, totalCount);
        //        }
        //        else if (successCount > 0)
        //        {
        //            _logger.LogWarning("⚠️ MCP CONNECTIVITY TEST: PARTIAL SUCCESS ({SuccessCount}/{TotalCount} agents connected)", 
        //                successCount, totalCount);
        //        }
        //        else
        //        {
        //            _logger.LogError("💥 MCP CONNECTIVITY TEST: COMPLETE FAILURE (0/{TotalCount} agents connected) - CHECK MCP SERVER!", 
        //                totalCount);
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogError(ex, "💥 MCP Connectivity test failed with exception: {ExceptionMessage}", ex.Message);
        //    }
        //}

        public async ValueTask DisposeAsync()
        {
            // Dispose all MCP clients
            foreach (var client in _mcpClients.Values)
            {
                if (client != null)
                    await client.DisposeAsync();
            }
            _mcpClients.Clear();
        }

        // Legacy sync dispose for IDisposable compatibility
        public void Dispose()
        {
            DisposeAsync().AsTask().GetAwaiter().GetResult();
        }
    }

    /// <summary>
    /// MCP OAuth Client using the actual ModelContextProtocol.Client package
    /// </summary>
    public class McpOAuthClient : IAsyncDisposable
    {
        private readonly string _authority;
        private readonly string _clientId;
        private readonly string _clientSecret;
        private readonly string _scope;
        private readonly string _baseUrl;
        private readonly ILogger? _logger;

        // Use proper MCP client from ModelContextProtocol package
        private object? _mcpClient; // This should be the actual MCP client type from the package
        private string? _accessToken;

        public McpOAuthClient(string authority, string clientId, string clientSecret, string scope, string baseUrl, ILogger? logger = null)
        {
            _authority = authority;
            _clientId = clientId;
            _clientSecret = clientSecret;
            _scope = scope;
            _baseUrl = baseUrl.TrimEnd('/');
            _logger = logger;
        }

        /// <summary>
        /// Acquires an OAuth access token from Azure AD using client credentials flow.
        /// </summary>
        private async Task<string> AcquireTokenAsync()
        {
            try
            {
                // Skip OAuth if no credentials provided (for testing/development)
                if (string.IsNullOrEmpty(_clientId) || string.IsNullOrEmpty(_clientSecret))
                {
                    _logger?.LogWarning("No OAuth credentials provided, skipping authentication for MCP client");
                    return "test-token";
                }

                var app = ConfidentialClientApplicationBuilder.Create(_clientId)
                    .WithAuthority(_authority)
                    .WithClientSecret(_clientSecret)
                    .Build();

                var result = await app.AcquireTokenForClient(new[] { _scope }).ExecuteAsync();
                return result.AccessToken;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to acquire OAuth token for MCP client");
                throw new InvalidOperationException("Failed to acquire OAuth token for MCP client", ex);
            }
        }

        /// <summary>
        /// Initializes the MCP client using the proper ModelContextProtocol.Client
        /// </summary>
        public async Task InitializeAsync()
        {
            try
            {
                _accessToken = await AcquireTokenAsync();

                // Create proper MCP client from ModelContextProtocol package
                // TODO: Replace with actual MCP client instantiation once package types are identified
                // This might be something like:
                // var mcpClientConfig = new McpClientConfiguration
                // {
                //     BaseUrl = _baseUrl,
                //     AccessToken = _accessToken
                // };
                // _mcpClient = new McpClient(mcpClientConfig);

                // For now, using HttpClient as fallback until proper MCP types are identified
                var httpClient = new HttpClient
                {
                    BaseAddress = new Uri(_baseUrl),
                    Timeout = TimeSpan.FromSeconds(30)
                };
                
                httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
                httpClient.DefaultRequestHeaders.Add("User-Agent", "MultiAgentCopilot-MCP-Client/1.0");
                
                if (!string.IsNullOrEmpty(_accessToken) && _accessToken != "test-token")
                {
                    httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_accessToken}");
                }

                _mcpClient = httpClient;

                _logger?.LogInformation("MCP OAuth client initialized successfully for {BaseUrl}", _baseUrl);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to initialize MCP OAuth client for {BaseUrl}", _baseUrl);
                throw;
            }
        }

        /// <summary>
        /// Lists available tools using the proper MCP client
        /// </summary>
        public async Task<IReadOnlyList<McpToolDefinition>> ListToolsAsync()
        {
            EnsureInitialized();

            try
            {
                // TODO: Use proper MCP client method like:
                // var tools = await _mcpClient.ListToolsAsync();
                // return tools;

                // Temporary implementation using HttpClient until proper MCP client is available
                var httpClient = (HttpClient)_mcpClient!;

                var mcpRequest = new
                {
                    jsonrpc = "2.0",
                    method = "tools/list",
                    id = Guid.NewGuid().ToString(),
                    @params = new { }
                };

                var requestJson = JsonSerializer.Serialize(mcpRequest);
                var content = new StringContent(requestJson, System.Text.Encoding.UTF8, "application/json");
                
                _logger?.LogDebug("📡 Sending MCP ListTools request: {JsonRequest}", requestJson);
                
                var response = await httpClient.PostAsync("/", content);
                
                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger?.LogError("❌ Failed to list MCP tools. Status: {StatusCode}, Error: {ErrorContent}", 
                        response.StatusCode, errorContent);
                    return new List<McpToolDefinition>();
                }

                var responseContent = await response.Content.ReadAsStringAsync();
                _logger?.LogDebug("📋 Raw MCP ListTools response: {ResponseContent}", responseContent);
                
                var mcpResponse = JsonSerializer.Deserialize<JsonElement>(responseContent);

                var tools = new List<McpToolDefinition>();
                
                if (mcpResponse.TryGetProperty("result", out var resultElement) &&
                    resultElement.TryGetProperty("tools", out var toolsArray))
                {
                    foreach (var toolElement in toolsArray.EnumerateArray())
                    {
                        var tool = new McpToolDefinition
                        {
                            Name = toolElement.GetProperty("name").GetString() ?? "",
                            Description = toolElement.GetProperty("description").GetString() ?? "",
                            InputSchema = toolElement.TryGetProperty("inputSchema", out var schemaElement) ? 
                                schemaElement.GetRawText() : "{}"
                        };

                        if (toolElement.TryGetProperty("McpToolTags", out var tagsElement))
                        {
                            var tagsString = tagsElement.GetString();
                            if (!string.IsNullOrEmpty(tagsString))
                            {
                                tool.Tags = tagsString.Split(',', StringSplitOptions.RemoveEmptyEntries)
                                           .Select(t => t.Trim()).ToArray();
                            }
                        }

                        tools.Add(tool);
                    }
                }

                return tools;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Exception in ListToolsAsync");
                return new List<McpToolDefinition>();
            }
        }

        /// <summary>
        /// Invokes a tool using the proper MCP client
        /// </summary>
        public async Task<McpToolResult> InvokeToolAsync(string toolName, Dictionary<string, object>? parameters = null)
        {
            EnsureInitialized();

            try
            {
                // TODO: Use proper MCP client method like:
                // var result = await _mcpClient.InvokeToolAsync(toolName, parameters);
                // return result;

                // Temporary implementation using HttpClient until proper MCP client is available
                var httpClient = (HttpClient)_mcpClient!;
                parameters ??= new Dictionary<string, object>();

                var mcpRequest = new
                {
                    jsonrpc = "2.0",
                    method = "tools/call",
                    id = Guid.NewGuid().ToString(),
                    @params = new
                    {
                        name = toolName,
                        arguments = parameters
                    }
                };

                var requestJson = JsonSerializer.Serialize(mcpRequest);
                _logger?.LogDebug("📡 Sending MCP InvokeTool request: {JsonRequest}", requestJson);
                
                var content = new StringContent(requestJson, System.Text.Encoding.UTF8, "application/json");
                
                var response = await httpClient.PostAsync("/", content);
                
                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger?.LogError("❌ Failed to invoke MCP tool {ToolName}. Status: {StatusCode}, Error: {ErrorContent}", 
                        toolName, response.StatusCode, errorContent);
                    return new McpToolResult
                    {
                        IsSuccess = false,
                        ErrorMessage = $"HTTP {response.StatusCode}: {errorContent}"
                    };
                }

                var responseContent = await response.Content.ReadAsStringAsync();
                _logger?.LogDebug("📋 Raw MCP InvokeTool response: {ResponseContent}", responseContent);
                
                var mcpResponse = JsonSerializer.Deserialize<JsonElement>(responseContent);
                
                if (mcpResponse.TryGetProperty("result", out var resultElement) &&
                    resultElement.TryGetProperty("content", out var contentArray))
                {
                    var firstContent = contentArray.EnumerateArray().FirstOrDefault();
                    if (firstContent.TryGetProperty("text", out var textElement))
                    {
                        return new McpToolResult
                        {
                            IsSuccess = true,
                            Content = textElement.GetString()
                        };
                    }
                }
                
                return new McpToolResult
                {
                    IsSuccess = true,
                    Content = responseContent
                };
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Exception in InvokeToolAsync for tool {ToolName}", toolName);
                return new McpToolResult
                {
                    IsSuccess = false,
                    ErrorMessage = ex.Message
                };
            }
        }

        private void EnsureInitialized()
        {
            if (_mcpClient == null)
                throw new InvalidOperationException("MCP Client not initialized. Call InitializeAsync() first.");
        }

        public async ValueTask DisposeAsync()
        {
            if (_mcpClient is HttpClient httpClient)
            {
                httpClient.Dispose();
                _mcpClient = null;
            }
            // TODO: Proper disposal when using actual MCP client:
            // if (_mcpClient is IMcpClient mcpClient)
            // {
            //     await mcpClient.DisposeAsync();
            //     _mcpClient = null;
            // }
        }
    }

    /// <summary>
    /// Represents an MCP tool definition
    /// </summary>
    public class McpToolDefinition
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string InputSchema { get; set; } = "{}";
        public string[] Tags { get; set; } = Array.Empty<string>();
    }

    /// <summary>
    /// Represents the result of an MCP tool invocation
    /// </summary>
    public class McpToolResult
    {
        public bool IsSuccess { get; set; }
        public string? Content { get; set; }
        public string? ErrorMessage { get; set; }
    }

    //// Simple MCP tool model
    //public class McpTool:McpClientTool
    //{
    //    public string Name { get; set; } = string.Empty;
    //    public string Description { get; set; } = string.Empty;
    //    public JsonElement InputSchema { get; set; }
    //    public List<string> Tags { get; set; } = new List<string>();
    //}

    // Configuration for each agent
    public class AgentConfiguration
    {
        public string Url { get; set; } = string.Empty;
        public string TokenEndpoint { get; set; } = string.Empty;
        public string ClientId { get; set; } = string.Empty;
        public string ClientSecret { get; set; } = string.Empty;
        public string Scope { get; set; } = string.Empty;
        public List<string> Tags { get; set; } = new List<string>();
    }

    /// <summary>
    /// Simple HTTP client factory implementation for dependency injection
    /// </summary>
    public class SimpleHttpClientFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name)
        {
            return new HttpClient();
        }
    }
}