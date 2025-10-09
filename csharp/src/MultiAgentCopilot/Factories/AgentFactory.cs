using Banking.Services;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using MultiAgentCopilot.Models;
using MultiAgentCopilot.MultiAgentCopilot.Services;
using MultiAgentCopilot.Services;
using MultiAgentCopilot.Tools;
using OpenAI;
using OpenAI.Chat;
using OpenAI.Responses;
using System.ComponentModel;
using System.Reflection;
using System.Text.Json;

namespace MultiAgentCopilot.Factories
{
    public static class AgentFactory
    {
        private static AIAgent CreateAgent(IChatClient chatClient, ILoggerFactory loggerFactory, string instructions, string? description = null, string? name = null, params AITool[] tools)
        {
            //// Correct constructor usage for ChatClientAgent:
            //var options = new ChatClientAgentOptions
            //{
            //    Instructions = instructions,
            //    Name = name,
            //    Description = description,
            //    ChatOptions = new() { Tools = functions, ToolMode = ChatToolMode.Auto }
            //};
            //return new ChatClientAgent(chatClient, options, loggerFactory);

            return chatClient.CreateAIAgent(instructions, name, description, tools);
        }

        /// <summary>
        /// Create all banking agents with proper instructions and tools
        /// </summary>
        public static List<AIAgent> CreateAllAgentsWithInProcessTools(IChatClient chatClient, BankingDataService bankService, ILoggerFactory loggerFactory)
        {
            var agents = new List<AIAgent>();
            ILogger logger = loggerFactory.CreateLogger("AgentFactory");

            // Get all agent types from the enum
            var agentTypes = Enum.GetValues<AgentType>();

            // Create agents for each agent type
            foreach (var agentType in agentTypes)
            {
                var agent = CreateAgent(
                    chatClient: chatClient,
                    loggerFactory: loggerFactory,
                    instructions: GetAgentPrompt(agentType),
                    name: GetAgentName(agentType),
                    description: GetAgentDescription(agentType),
                    tools: GetAgentTools(agentType, bankService, loggerFactory).ToArray()
                );

                agents.Add(agent);
                logger.LogInformation($"Created {agent.Name}: {agent.Description}");
            }

            logger.LogInformation("Successfully created {AgentCount} banking agents", agents.Count);
            return agents;
        }

        public static async Task<List<AIAgent>> CreateAllAgentsWithMCPToolsAsync(IChatClient chatClient, MCPToolService mcpService, ILoggerFactory loggerFactory)
        {
            var agents = new List<Microsoft.Agents.AI.AIAgent>();
            ILogger logger = loggerFactory.CreateLogger("AgentFactory");

            // Get all agent types from the enum
            var agentTypes = Enum.GetValues<AgentType>();

            // Create agents for each agent type with proper MCP integration
            foreach (var agentType in agentTypes)
            {
                try
                {
                    logger.LogInformation("Creating agent {AgentType} with MCP tools", agentType);

                    // Convert MCP tools to AI functions using proper async MCP client patterns
                    var aiFunctions = await ConvertMcpToolsToAIFunctionsAsync(mcpService, agentType, logger).ConfigureAwait(false);

                    var agent = CreateAgent(
                        chatClient: chatClient,
                        loggerFactory: loggerFactory,
                        instructions: GetAgentPrompt(agentType),
                        name: GetAgentName(agentType),
                        description: GetAgentDescription(agentType),
                        tools: aiFunctions.ToArray()
                    );

                    agents.Add(agent);
                    logger.LogInformation("Created agent {AgentName} with {ToolCount} MCP tools", agent.Name, aiFunctions.Count());
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to create agent {AgentType} with MCP tools", agentType);
                }
            }

            return agents;
        }

        /// <summary>
        /// Convert MCP tools to AI functions for use with agents using proper MCP client
        /// </summary>
        private static async Task<IEnumerable<AITool>> ConvertMcpToolsToAIFunctionsAsync(MCPToolService mcpService, AgentType agentType, ILogger logger)
        { 
            try
            {
                logger.LogInformation("Retrieving MCP tools for agent {AgentType}", agentType);

                // Get MCP tools using the proper MCP client approach
                return await mcpService.GetMcpTools(agentType).ConfigureAwait(false);

            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to retrieve MCP tools for agent {AgentType}", agentType, ex);
            }

            return Enumerable.Empty<AITool>();
        }

        /// <summary>
        /// Create a dynamic method for the MCP tool with proper attributes
        /// </summary>
        //private static System.Reflection.MethodInfo? CreateDynamicToolMethod(McpToolExecutor executor, McpTool mcpTool)
        //{
        //    // For now, use the existing ExecuteAsync method
        //    // In a more advanced implementation, we could create dynamic methods with proper signatures
        //    var method = typeof(McpToolExecutor).GetMethod(nameof(McpToolExecutor.ExecuteAsync));

        //    // We could enhance this to create methods with proper parameter signatures based on the MCP tool schema
        //    return method;
        //}

        ///// <summary>
        ///// Proper MCP tool executor that integrates with Microsoft.Extensions.AI.Agents
        ///// </summary>
        //private class McpToolExecutor
        //{
        //    private readonly MCPToolService _mcpService;
        //    private readonly AgentType _agentType;
        //    private readonly McpTool _mcpTool;
        //    private readonly ILogger _logger;

        //    public McpToolExecutor(MCPToolService mcpService, AgentType agentType, McpTool mcpTool, ILogger logger)
        //    {
        //        _mcpService = mcpService;
        //        _agentType = agentType;
        //        _mcpTool = mcpTool;
        //        _logger = logger;
        //    }

        //    [Description("Execute MCP banking tool")]
        //    public async Task<string> ExecuteAsync(Dictionary<string, object>? arguments = null)
        //    {
        //        try
        //        {
        //            _logger.LogInformation("Executing MCP tool {ToolName} for agent {AgentType}", _mcpTool.Name, _agentType);

        //            // Use the MCP service to execute the tool with proper MCP client patterns
        //            var result = await _mcpService.CallToolAsync(_agentType, _mcpTool.Name, arguments);

        //            // Handle different result types appropriately
        //            return result switch
        //            {
        //                string stringResult => stringResult,
        //                null => "Tool execution completed successfully",
        //                _ => System.Text.Json.JsonSerializer.Serialize(result, new System.Text.Json.JsonSerializerOptions { WriteIndented = true })
        //            };
        //        }
        //        catch (Exception ex)
        //        {
        //            _logger.LogError(ex, "Error executing MCP tool {ToolName} for agent {AgentType}", _mcpTool.Name, _agentType);
        //            return $"Error executing {_mcpTool.Name}: {ex.Message}";
        //        }
        //    }
        //}

        ///// <summary>
        ///// Extract parameters from MCP tool definition - Simplified for current API
        ///// </summary>
        //private static IEnumerable<object> ExtractParametersFromMcpTool(McpTool mcpTool, ILogger logger)
        //{
        //    // Parameters are extracted automatically by the AI framework from method signatures
        //    // This method is kept for potential future enhancements
        //    return Enumerable.Empty<object>();
        //}

        /// <summary>
        /// Extract parameters from MCP tool definition - Legacy method
        /// </summary>
        //private static IEnumerable<object> GetParametersFromMcpTool(McpTool mcpTool)
        //{
        //    return Enumerable.Empty<object>();
        //}

        /// <summary>
        /// Get agent prompt based on type
        /// </summary>
        private static string GetAgentPrompt(AgentType agentType)
        {
            string promptFile = agentType switch
            {
                AgentType.Sales => "Sales.prompty",
                AgentType.Transactions => "Transactions.prompty",
                AgentType.CustomerSupport => "CustomerSupport.prompty",
                AgentType.Coordinator => "Coordinator.prompty",
                _ => throw new ArgumentOutOfRangeException(nameof(agentType), agentType, null)
            };

            string prompt = $"{File.ReadAllText($"Prompts/{promptFile}")}{File.ReadAllText("Prompts/CommonAgentRules.prompty")}";

            return prompt;
        }

        /// <summary>
        /// Get agent name based on type
        /// Names must match pattern: ^[^\s<|\\/>]+$ (no spaces or special characters)
        /// </summary>
        private static string GetAgentName(AgentType agentType)
        {
            return agentType switch
            {
                AgentType.Sales => "Sales",
                AgentType.Transactions => "Transactions",
                AgentType.CustomerSupport => "CustomerSupport",
                AgentType.Coordinator => "Coordinator",
                _ => throw new ArgumentOutOfRangeException(nameof(agentType), agentType, null)
            };
        }

        /// <summary>
        /// Get agent description
        /// </summary>
        private static string GetAgentDescription(AgentType agentType)
        {
            return agentType switch
            {
                AgentType.Sales => "Handles sales inquiries, account registration, and offers",
                AgentType.Transactions => "Manages transactions, transfers, and transaction history",
                AgentType.CustomerSupport => "Provides customer support, handles complaints and service requests",
                AgentType.Coordinator => "Coordinates and routes requests to appropriate agents",
                _ => throw new ArgumentOutOfRangeException(nameof(agentType), agentType, null)
            };
        }

        /// <summary>
        /// Get tools for specific agent type using existing tool classes
        /// Returns null for now due to delegate binding limitations with the current Agent Framework API
        /// </summary>
        private static IList<AIFunction>? GetAgentTools(AgentType agentType, BankingDataService bankService, ILoggerFactory loggerFactory)
        {
            ILogger logger = loggerFactory.CreateLogger<AgentFrameworkService>();
            try
            {
                logger.LogInformation("Creating tools for agent type: {AgentType}", agentType);

                // Create the appropriate tools class based on agent type
                BaseTools toolsClass = agentType switch
                {
                    AgentType.Sales => new SalesTools(loggerFactory.CreateLogger<SalesTools>(), bankService),
                    AgentType.Transactions => new TransactionTools(loggerFactory.CreateLogger<TransactionTools>(), bankService),
                    AgentType.CustomerSupport => new CustomerSupportTools(loggerFactory.CreateLogger<CustomerSupportTools>(), bankService),
                    AgentType.Coordinator => new CoordinatorTools(loggerFactory.CreateLogger<CoordinatorTools>(), bankService),
                    _ => throw new ArgumentOutOfRangeException(nameof(agentType), agentType, null)
                };

                // Log the tool class creation for debugging
                logger.LogInformation("Created {ToolClassName} for agent type: {AgentType}", toolsClass.GetType().Name, agentType);

                // Log available methods with Description attributes
                var methods = toolsClass.GetType().GetMethods()
                    .Where(m => m.GetCustomAttributes(typeof(DescriptionAttribute), false).Length > 0);



                IList<AIFunction> functions = new List<AIFunction>();
                foreach (var method in methods)
                {
                    functions.Add(AIFunctionFactory.Create(method, toolsClass));
                    var description = method.GetCustomAttribute<DescriptionAttribute>()?.Description ?? "No description";
                    logger.LogInformation("Agent {AgentType} has method: '{MethodName}' - {Description}",
                        agentType, method.Name, description);
                }

                logger.LogInformation("Tool class created for agent type: {AgentType}. Returning null due to delegate binding limitations.", agentType);

                return functions;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error creating tools for agent type: {AgentType}", agentType);
                return null;
            }
        }
    }
}