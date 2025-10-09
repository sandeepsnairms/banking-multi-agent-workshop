using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Client;
using MultiAgentCopilot.Models;
using MultiAgentCopilot.Models.Configuration;
using MultiAgentCopilot.MultiAgentCopilot.Models.Configuration;
using MultiAgentCopilot.Transport;
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

            return serverSettings;
        }

        public MCPServerSettings GetMCPServerSettingsPublic(AgentType agentType)
        {
            return GetMCPServerSettings(agentType);
        }

        public async Task<IList<McpClientTool>> GetMcpTools(AgentType agent)
        {
            _logger.LogInformation("🔍 DEBUG: GetMcpTools starting for agent: {AgentType}", agent);

            try
            {
                // Get agent configuration
                _logger.LogInformation("🔧 DEBUG: Getting MCP server settings for agent: {AgentType}", agent);
                var settings = GetMCPServerSettings(agent);
                _logger.LogInformation("✅ DEBUG: Got settings - Url: {Url}, Key: {KeyLength} chars", 
                    settings.Url, settings.Key?.Length ?? 0);

                // Use our factory to create an authenticated transport
                _logger.LogInformation("🔧 DEBUG: Creating authenticated HTTP wrapper...");
                var wrapper = new AuthenticatedHttpWrapper(settings.Url, settings.Key);
                
                _logger.LogInformation("🔧 DEBUG: Creating transport...");
                IClientTransport clientTransport = wrapper.CreateTransport();
                _logger.LogInformation("✅ DEBUG: Transport created successfully");
                
                _logger.LogInformation("🔧 DEBUG: Creating MCP client...");
                await using var mcpClient = await McpClient.CreateAsync(clientTransport!);
                _logger.LogInformation("✅ DEBUG: MCP client created successfully");

                _logger.LogInformation("🔧 DEBUG: Calling ListToolsAsync...");
                var tools = await mcpClient.ListToolsAsync();
                _logger.LogInformation("✅ DEBUG: ListToolsAsync completed - Found {ToolCount} tools", tools.Count);

                // Log each tool for debugging
                foreach (var tool in tools)
                {
                    _logger.LogDebug("📋 DEBUG: Tool found - Name: {ToolName}, Description: {ToolDescription}", 
                        tool.Name, tool.Description);
                }

                return tools;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "💥 DEBUG: Exception in GetMcpTools for agent {AgentType}: {ExceptionMessage} | StackTrace: {StackTrace}", 
                    agent, ex.Message, ex.StackTrace);
                return new List<McpClientTool>();
            }
        }

        public async ValueTask DisposeAsync()
        {
            // No resources to dispose in this cleaned up version
            await Task.CompletedTask;
        }

        public void Dispose()
        {
            // Synchronous dispose for compatibility
            GC.SuppressFinalize(this);
        }
    }
}