using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using MultiAgentCopilot.Models;
using MultiAgentCopilot.Models.Configuration;
using MultiAgentCopilot.MultiAgentCopilot.Models.Configuration;
using MultiAgentCopilot.Services;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net.Http;
using System.Runtime;
using System.Text.Json;
using System.Threading.Tasks;

namespace MultiAgentCopilot.MultiAgentCopilot.Services
{
    public class MCPToolService : IDisposable
    {
        private HttpClient? _httpClient;
        private readonly ILogger<MCPToolService> _logger;
        private MCPSettings _mcpSettings;


        public MCPToolService(MCPSettings mcpSettings, ILogger<MCPToolService> logger)
        {
            _mcpSettings = mcpSettings ?? throw new ArgumentNullException(nameof(mcpSettings));
            _logger = logger;
            
            _logger.LogInformation("Initialized MCPToolFactory with settings: ConnectionType={ConnectionType}", mcpSettings.ConnectionType);
        }

        private AgentConfiguration GetAgentConfiguration(AgentType agentType)
        {
            var agentName = agentType.ToString().ToUpper();

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

        public async Task<IList<McpClientTool>> GetMcpTools(AgentType agent)
        {
            IClientTransport clientTransport;
            _logger.LogInformation("Retrieving MCP tools for agent: {AgentType}", agent);


            var jwtToken = _mcpSettings.JWTTokenSecret;
            var mcpConnectionType = _mcpSettings.ConnectionType;

            // Load agent configuration on-demand
            var agentConfig = GetAgentConfiguration(agent);

            _logger.LogInformation("MCPToolFactory  Connection type: {ConnectionType}", mcpConnectionType);

            if (mcpConnectionType == MCPConnectionType.HTTP)
            {
                // Create HttpClient with JWT Bearer token
                _httpClient = new HttpClient();
                _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", jwtToken);
                _httpClient.DefaultRequestHeaders.Add("Accept", "application/json");

                clientTransport = new HttpClientTransport(new()
                {
                    Endpoint = new Uri(agentConfig.Url)
                });
            }
            else
            {
                clientTransport = new StdioClientTransport(new()
                {
                    Name = $"Banking Tools Server - {agent}",
                    Command = agentConfig.Url
                });
            }

            try
            {
                await using var mcpClient = await McpClient.CreateAsync(clientTransport!);

                var mcpTools = await mcpClient.ListToolsAsync();

                // Filter tools based on agent tags if configured
                var filteredTools = FilterToolsByAgentTags(mcpTools, agentConfig.Tags);

                _logger.LogInformation("Retrieved {TotalTools} tools, filtered to {FilteredTools} for agent {AgentType} using tags [{Tags}]",
                    mcpTools.Count, filteredTools.Count, agent, string.Join(", ", agentConfig.Tags));

                return filteredTools;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve MCP tools for agent {AgentType}", agent);
                return new List<McpClientTool>();
            }
        }

        private IList<McpClientTool> FilterToolsByAgentTags(
            IList<McpClientTool> allTools,
            List<string> agentTags)
        {
            // If agent has no tags configured, return all tools
            if (agentTags == null || agentTags.Count == 0)
            {
                _logger.LogDebug("Agent has no tags configured, returning all {ToolCount} tools", allTools.Count);
                return allTools;
            }

            var filteredTools = new List<McpClientTool>();

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

        private bool HasMatchingTags(McpClientTool tool, List<string> agentTags)
        {
            // assume `tools` is IEnumerable<McpClientTool> returned by client.ListTools()
           
            Console.WriteLine($"Tool: {tool.Name}");
            Console.WriteLine($"  Description: {tool.Description}");

            tool.AdditionalProperties.TryGetValue("McpToolTags", out var tagElements);

            _logger.LogDebug("Tool {ToolName} has tags: {TagsJson}", tool.Name, tagElements?.ToString() ?? "[]");

            // If no tags found in tool, return false
            if (tagElements == null)
            {
                _logger.LogDebug("Tool {ToolName} has no tags defined, skipping tag match, returning true.", tool.Name);
                return true;
            }

            try
            {
                // Extract tool tags from the CSV string
                var toolTags = new List<string>();
                
                // tagElements is a CSV string
                var csvString = tagElements.ToString();
                if (!string.IsNullOrEmpty(csvString))
                {
                    toolTags.AddRange(csvString.Split(',', StringSplitOptions.RemoveEmptyEntries)
                                              .Select(tag => tag.Trim())
                                              .Where(tag => !string.IsNullOrEmpty(tag)));
                }

                _logger.LogDebug("Extracted tool tags for {ToolName}: [{ToolTags}]", tool.Name, string.Join(", ", toolTags));

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
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error processing tags for tool {ToolName}", tool.Name);
                return false;
            }
        }
        

        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }

    // Configuration for each agent
    public class AgentConfiguration
    {
        public string Url { get; set; } = string.Empty;
        public List<string> Tags { get; set; } = new List<string>();
    }
}