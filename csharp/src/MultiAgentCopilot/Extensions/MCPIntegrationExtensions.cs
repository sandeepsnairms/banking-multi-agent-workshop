using MultiAgentCopilot.Factories;
using MultiAgentCopilot.Models;
using MultiAgentCopilot.MultiAgentCopilot.Services;
using Microsoft.Extensions.Logging;

namespace MultiAgentCopilot.Extensions
{
    /// <summary>
    /// Extension methods for MCP integration diagnostics and utilities
    /// </summary>
    public static class MCPIntegrationExtensions
    {
        /// <summary>
        /// Logs detailed MCP diagnostics information
        /// </summary>
        public static void LogMCPDiagnostics(this ILogger logger, Dictionary<AgentType, AgentMCPDiagnostics> diagnostics)
        {
            logger.LogInformation("?? MCP Integration Diagnostics Summary");
            logger.LogInformation("=====================================");

            foreach (var (agentType, diagnostic) in diagnostics)
            {
                var status = diagnostic.IsConnected ? "? Connected" : 
                           diagnostic.IsConfigured ? "?? Configured but disconnected" : 
                           "? Not configured";

                logger.LogInformation("Agent: {AgentName} ({AgentType}) - {Status}",
                    diagnostic.AgentName, agentType, status);

                if (!string.IsNullOrEmpty(diagnostic.ServerUrl))
                {
                    logger.LogInformation("  Server URL: {ServerUrl}", diagnostic.ServerUrl);
                }

                if (diagnostic.IsConnected)
                {
                    logger.LogInformation("  Tools Available: {ToolCount}", diagnostic.ToolCount);
                    
                    if (diagnostic.Tools.Any())
                    {
                        logger.LogDebug("  Tool Details:");
                        foreach (var tool in diagnostic.Tools.Take(5)) // Limit to first 5 for readability
                        {
                            logger.LogDebug("    - {Tool}", tool);
                        }
                        
                        if (diagnostic.Tools.Count > 5)
                        {
                            logger.LogDebug("    ... and {AdditionalTools} more tools", diagnostic.Tools.Count - 5);
                        }
                    }
                }

                if (!string.IsNullOrEmpty(diagnostic.ErrorMessage))
                {
                    logger.LogWarning("  Error: {ErrorMessage}", diagnostic.ErrorMessage);
                }

                logger.LogInformation(""); // Empty line for readability
            }

            // Summary statistics
            var totalAgents = diagnostics.Count;
            var configuredAgents = diagnostics.Values.Count(d => d.IsConfigured);
            var connectedAgents = diagnostics.Values.Count(d => d.IsConnected);
            var totalTools = diagnostics.Values.Sum(d => d.ToolCount);

            logger.LogInformation("?? Summary Statistics:");
            logger.LogInformation("  Total Agents: {TotalAgents}", totalAgents);
            logger.LogInformation("  Configured: {ConfiguredAgents}", configuredAgents);
            logger.LogInformation("  Connected: {ConnectedAgents}", connectedAgents);
            logger.LogInformation("  Total Tools Available: {TotalTools}", totalTools);
            logger.LogInformation("=====================================");
        }

        /// <summary>
        /// Gets a summary of MCP integration health
        /// </summary>
        public static MCPIntegrationHealth GetIntegrationHealth(this Dictionary<AgentType, AgentMCPDiagnostics> diagnostics)
        {
            var health = new MCPIntegrationHealth();

            foreach (var diagnostic in diagnostics.Values)
            {
                health.TotalAgents++;
                
                if (diagnostic.IsConfigured)
                    health.ConfiguredAgents++;
                
                if (diagnostic.IsConnected)
                    health.ConnectedAgents++;
                
                health.TotalTools += diagnostic.ToolCount;

                if (!string.IsNullOrEmpty(diagnostic.ErrorMessage))
                    health.Errors.Add($"{diagnostic.AgentName}: {diagnostic.ErrorMessage}");
            }

            health.HealthScore = health.TotalAgents > 0 ? 
                (double)health.ConnectedAgents / health.TotalAgents * 100 : 0;

            return health;
        }

        /// <summary>
        /// Validates that all required agents are properly configured
        /// </summary>
        public static async Task<bool> ValidateRequiredAgentsAsync(this MCPToolService mcpService, 
            AgentType[] requiredAgents, ILogger logger)
        {
            logger.LogInformation("?? Validating required agents: {RequiredAgents}", 
                string.Join(", ", requiredAgents));

            var allValid = true;

            foreach (var agentType in requiredAgents)
            {
                try
                {
                    var isConnected = await mcpService.TestConnectionAsync(agentType);
                    
                    if (isConnected)
                    {
                        logger.LogInformation("? Required agent {AgentType} is available", agentType);
                    }
                    else
                    {
                        logger.LogError("? Required agent {AgentType} is not available", agentType);
                        allValid = false;
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "? Error validating required agent {AgentType}: {Message}", 
                        agentType, ex.Message);
                    allValid = false;
                }
            }

            return allValid;
        }
    }

    /// <summary>
    /// Health information for MCP integration
    /// </summary>
    public class MCPIntegrationHealth
    {
        public int TotalAgents { get; set; }
        public int ConfiguredAgents { get; set; }
        public int ConnectedAgents { get; set; }
        public int TotalTools { get; set; }
        public double HealthScore { get; set; }
        public List<string> Errors { get; set; } = new();

        public bool IsHealthy => HealthScore >= 80 && Errors.Count == 0;
        public bool IsPartiallyHealthy => HealthScore >= 50;
    }
}