using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Client;
using MultiAgentCopilot.Models;
using MultiAgentCopilot.Models.Configuration;
using MultiAgentCopilot.MultiAgentCopilot.Models.Configuration;
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
        private readonly Dictionary<AgentType, IClientTransport> _transports = new();
        private readonly Dictionary<AgentType, McpClient> _clients = new();

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
                _logger.LogWarning("⚠️ No MCP servers configured in MCPSettings.Servers");
                return;
            }

            foreach (var server in _mcpSettings.Servers)
            {
                if (string.IsNullOrWhiteSpace(server.AgentName))
                {
                    _logger.LogWarning("⚠️ MCP server configuration has empty AgentName");
                }

                if (string.IsNullOrWhiteSpace(server.Url))
                {
                    _logger.LogWarning("⚠️ MCP server '{AgentName}' has empty Url", server.AgentName);
                }

                if (string.IsNullOrWhiteSpace(server.Key))
                {
                    _logger.LogWarning("⚠️ MCP server '{AgentName}' has empty API Key", server.AgentName);
                }

                // Validate URL format
                if (!string.IsNullOrWhiteSpace(server.Url) && !Uri.TryCreate(server.Url, UriKind.Absolute, out _))
                {
                    _logger.LogWarning("⚠️ MCP server '{AgentName}' has invalid Url format: {Url}", server.AgentName, server.Url);
                }
            }

            _logger.LogInformation("✅ MCP configuration validated for {ServerCount} servers", _mcpSettings.Servers.Count);
        }

        private MCPServerSettings GetMCPServerSettings(AgentType agentType)
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

            // Validate the server settings
            if (string.IsNullOrWhiteSpace(serverSettings.Url))
            {
                throw new InvalidOperationException($"MCP server URL for agent '{agentName}' is not configured.");
            }

            if (string.IsNullOrWhiteSpace(serverSettings.Key))
            {
                throw new InvalidOperationException($"MCP server API key for agent '{agentName}' is not configured.");
            }

            return serverSettings;
        }

        public MCPServerSettings GetMCPServerSettingsPublic(AgentType agentType)
        {
            return GetMCPServerSettings(agentType);
        }

        /// <summary>
        /// Creates or retrieves a cached MCP client for the specified agent type
        /// </summary>
        private async Task<McpClient> GetOrCreateMcpClientAsync(AgentType agentType)
        {
            if (_clients.TryGetValue(agentType, out var existingClient))
            {
                _logger.LogDebug("🔄 Using cached MCP client for agent: {AgentType}", agentType);
                return existingClient;
            }

            _logger.LogInformation("🔧 Creating new MCP client for agent: {AgentType}", agentType);

            // Get agent configuration
            var settings = GetMCPServerSettings(agentType);

            // Create authenticated transport with enhanced error handling
            IClientTransport clientTransport;
            try
            {
                // Create transport with authentication and streamable-http support
                var wrapper = new AuthenticatedHttpWrapper(settings.Url, settings.Key);
                clientTransport = wrapper.CreateTransport();
                
                // Configure transport for streamable-http if needed
                if (clientTransport is HttpClientTransport httpTransport)
                {
                    await ConfigureTransportForStreamableHttp(httpTransport, settings);
                }

                _transports[agentType] = clientTransport;
                _logger.LogDebug("✅ Transport created successfully for agent: {AgentType}", agentType);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "💥 Failed to create transport for agent {AgentType}: {Message}", agentType, ex.Message);
                throw new InvalidOperationException($"Failed to create MCP transport for agent '{agentType}': {ex.Message}", ex);
            }

            // Create MCP client
            try
            {
                var mcpClient = await McpClient.CreateAsync(clientTransport);
                _clients[agentType] = mcpClient;
                _logger.LogInformation("✅ MCP client created successfully for agent: {AgentType}", agentType);
                return mcpClient;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "💥 Failed to create MCP client for agent {AgentType}: {Message}", agentType, ex.Message);
                
                // Clean up transport if client creation failed
                if (_transports.TryGetValue(agentType, out var transport))
                {
                    if (transport is IAsyncDisposable asyncDisposable)
                        await asyncDisposable.DisposeAsync();
                    else if (transport is IDisposable disposable)
                        disposable.Dispose();
                    
                    _transports.Remove(agentType);
                }

                throw new InvalidOperationException($"Failed to create MCP client for agent '{agentType}': {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Configures the HTTP transport for streamable-http support
        /// </summary>
        private async Task ConfigureTransportForStreamableHttp(HttpClientTransport transport, MCPServerSettings settings)
        {
            try
            {
                _logger.LogDebug("🔧 Configuring transport for streamable-http support...");

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
                        _logger.LogWarning("⚠️ X-MCP-API-Key header not found, adding manually...");
                        httpClient.DefaultRequestHeaders.Add("X-MCP-API-Key", settings.Key);
                    }

                    // Set appropriate timeout for MCP operations
                    if (httpClient.Timeout == System.Threading.Timeout.InfiniteTimeSpan)
                    {
                        httpClient.Timeout = TimeSpan.FromSeconds(30);
                    }

                    _logger.LogDebug("✅ Transport configured for streamable-http");
                }
                else
                {
                    _logger.LogWarning("⚠️ Could not access HttpClient to configure streamable-http support");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "⚠️ Failed to configure transport for streamable-http: {Message}", ex.Message);
                // Non-fatal error, continue with default configuration
            }

            await Task.CompletedTask;
        }

        public async Task<IList<McpClientTool>> GetMcpTools(AgentType agent)
        {
            _logger.LogInformation("🔍 Getting MCP tools for agent: {AgentType}", agent);

            try
            {
                // Get or create MCP client with authentication and streamable-http support
                var mcpClient = await GetOrCreateMcpClientAsync(agent);

                // List available tools from the MCP server
                var tools = await mcpClient.ListToolsAsync();
                _logger.LogInformation("✅ Retrieved {ToolCount} tools for agent {AgentType}", tools.Count, agent);

                // Log each tool for debugging
                foreach (var tool in tools)
                {
                    _logger.LogDebug("📋 Tool available - Name: {ToolName}, Description: {ToolDescription}", 
                        tool.Name, tool.Description);
                }

                return tools;
            }
            catch (InvalidOperationException)
            {
                // Configuration errors - already logged, re-throw
                throw;
            }
            catch (HttpRequestException httpEx)
            {
                _logger.LogError(httpEx, "🌐 HTTP error when connecting to MCP server for agent {AgentType}: {Message}", 
                    agent, httpEx.Message);
                
                // Check if it's an authentication error
                if (httpEx.Message.Contains("401") || httpEx.Message.Contains("Unauthorized"))
                {
                    _logger.LogError("🔐 Authentication failed - check X-MCP-API-Key configuration for agent {AgentType}", agent);
                }

                return new List<McpClientTool>();
            }
            catch (TaskCanceledException timeoutEx)
            {
                _logger.LogError(timeoutEx, "⏱️ Timeout when connecting to MCP server for agent {AgentType}: {Message}", 
                    agent, timeoutEx.Message);
                return new List<McpClientTool>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "💥 Unexpected error getting MCP tools for agent {AgentType}: {Message} | StackTrace: {StackTrace}", 
                    agent, ex.Message, ex.StackTrace);
                return new List<McpClientTool>();
            }
        }

        /// <summary>
        /// Tests the connection to the MCP server for a specific agent
        /// </summary>
        public async Task<bool> TestConnectionAsync(AgentType agentType)
        {
            _logger.LogInformation("🔍 Testing MCP connection for agent: {AgentType}", agentType);

            try
            {
                var mcpClient = await GetOrCreateMcpClientAsync(agentType);
                var tools = await mcpClient.ListToolsAsync();
                
                _logger.LogInformation("✅ Connection test successful for agent {AgentType} - {ToolCount} tools available", 
                    agentType, tools.Count);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Connection test failed for agent {AgentType}: {Message}", agentType, ex.Message);
                return false;
            }
        }

        /// <summary>
        /// Gets connection status for all configured agents
        /// </summary>
        public async Task<Dictionary<AgentType, bool>> GetConnectionStatusAsync()
        {
            var status = new Dictionary<AgentType, bool>();
            var allAgents = Enum.GetValues<AgentType>();

            foreach (var agent in allAgents)
            {
                try
                {
                    // Check if we have configuration for this agent
                    GetMCPServerSettings(agent);
                    status[agent] = await TestConnectionAsync(agent);
                }
                catch (InvalidOperationException)
                {
                    // No configuration for this agent
                    status[agent] = false;
                }
            }

            return status;
        }

        public async ValueTask DisposeAsync()
        {
            _logger.LogInformation("🧹 Disposing MCPToolService...");

            // Dispose all MCP clients
            foreach (var (agentType, client) in _clients)
            {
                try
                {
                    await client.DisposeAsync();
                    _logger.LogDebug("✅ Disposed MCP client for agent: {AgentType}", agentType);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "⚠️ Error disposing MCP client for agent {AgentType}: {Message}", agentType, ex.Message);
                }
            }
            _clients.Clear();

            // Dispose all transports
            foreach (var (agentType, transport) in _transports)
            {
                try
                {
                    if (transport is IAsyncDisposable asyncDisposable)
                    {
                        await asyncDisposable.DisposeAsync();
                    }
                    else if (transport is IDisposable disposable)
                    {
                        disposable.Dispose();
                    }
                    _logger.LogDebug("✅ Disposed transport for agent: {AgentType}", agentType);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "⚠️ Error disposing transport for agent {AgentType}: {Message}", agentType, ex.Message);
                }
            }
            _transports.Clear();

            _logger.LogInformation("✅ MCPToolService disposed successfully");
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
                _logger.LogWarning(ex, "⚠️ Error during synchronous dispose: {Message}", ex.Message);
            }
            
            GC.SuppressFinalize(this);
        }
    }
}