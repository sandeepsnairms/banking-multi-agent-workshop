using Microsoft.Extensions.AI;
using MultiAgentCopilot.Models;
using Microsoft.Agents.AI;
using BankingModels;
using MultiAgentCopilot.MultiAgentCopilot.Services;
using MultiAgentCopilot.Services;
using MultiAgentCopilot.Tools;
using OpenAI.Chat;
using System.ComponentModel;
using System.Reflection;
using System.Text.Json;
using OpenAI;

namespace MultiAgentCopilot.Factories
{
    public static class AgentFactory
    {
        private static AIAgent CreateAgent(OpenAI.Chat.ChatClient chatClient, ILoggerFactory loggerFactory, string instructions, string? description = null, string? name = null, params AIFunction[] functions)
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

            return chatClient.CreateAIAgent(instructions, name, description, functions);   
        }

        /// <summary>
        /// Create all banking agents with proper instructions and tools
        /// </summary>
        public static List<AIAgent> CreateAllAgentsWithInProcessTools(OpenAI.Chat.ChatClient chatClient, MockBankingService bankService, ILoggerFactory loggerFactory)
        {
            var agents = new List<AIAgent>();
            //ILogger logger = loggerFactory.CreateLogger("AgentFactory");

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
                    functions: GetAgentTools(agentType, bankService, loggerFactory).ToArray()
                );

                agents.Add(agent);
                Console.WriteLine($"Created {agent.Name}: {agent.Description}");
            }

            Console.WriteLine($"Successfully created {agents.Count} banking agents");
            return agents;
        }

        public static async Task<List<AIAgent>> CreateAllAgentsWithMCPToolsAsync(OpenAI.Chat.ChatClient chatClient, MCPToolService mcpService, ILoggerFactory loggerFactory)
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
                    Console.WriteLine($"Creating agent {agentType} with MCP tools");
                    
                    // Convert MCP tools to AI functions using proper async MCP client patterns
                    var aiFunctions = await ConvertMcpToolsToAIFunctionsAsync(mcpService, agentType, logger).ConfigureAwait(false);

                    var agent = CreateAgent(
                        chatClient: chatClient,
                        loggerFactory: loggerFactory,
                        instructions: GetAgentPrompt(agentType),
                        name: GetAgentName(agentType),
                        description: GetAgentDescription(agentType),
                        functions: aiFunctions.ToArray()
                    );

                    agents.Add(agent);
                    Console.WriteLine($"Created agent {agent.Name} with {aiFunctions.Count()} MCP tools");
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to create agent {AgentType} with MCP tools", agentType);
                    
                    // Fallback: create agent without MCP tools
                    try
                    {
                        var agent = CreateAgent(
                            chatClient: chatClient,
                            loggerFactory: loggerFactory,
                            instructions: GetAgentPrompt(agentType),
                            name: GetAgentName(agentType),
                            description: GetAgentDescription(agentType),
                            functions: Array.Empty<AIFunction>()
                        );

                        agents.Add(agent);
                        logger.LogWarning("Created agent {AgentName} without MCP tools due to error", agent.Name);
                    }
                    catch (Exception fallbackEx)
                    {
                        logger.LogError(fallbackEx, "Failed to create agent {AgentType} even without MCP tools", agentType);
                    }
                }
            }

            Console.WriteLine($"Successfully created {agents.Count} banking agents with MCP tools integration");
            return agents;
        }

        /// <summary>
        /// Convert MCP tools to AI functions for use with agents using proper MCP client
        /// </summary>
        private static async Task<IEnumerable<AIFunction>> ConvertMcpToolsToAIFunctionsAsync(MCPToolService mcpService, AgentType agentType, ILogger logger)
        {
            var functions = new List<AIFunction>();
            
            try
            {
                Console.WriteLine($"Retrieving MCP tools for agent {agentType}");

                // Get MCP tools using the proper MCP client approach
                var mcpTools = await mcpService.GetMcpTools(agentType).ConfigureAwait(false);
                
                foreach (var mcpTool in mcpTools)
                {
                    try
                    {
                        logger.LogDebug("Converting MCP tool {ToolName} to AI function for agent {AgentType}", mcpTool.Name, agentType);
                        
                        // Create a proper MCP tool wrapper that follows Microsoft.Extensions.AI.Agents patterns
                        var mcpToolExecutor = new McpToolExecutor(mcpService, agentType, mcpTool, logger);
                        
                        // Create a wrapper method with proper attributes
                        var wrapperMethod = CreateDynamicToolMethod(mcpToolExecutor, mcpTool);
                        
                        if (wrapperMethod != null)
                        {
                            // Create AI function using the standard factory pattern
                            var aiFunction = AIFunctionFactory.Create(wrapperMethod, mcpToolExecutor);
                            functions.Add(aiFunction);
                            logger.LogDebug("Successfully converted MCP tool {ToolName} to AI function", mcpTool.Name);
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Failed to convert MCP tool {ToolName} to AI function for agent {AgentType}", mcpTool.Name, agentType);
                    }
                }

                Console.WriteLine($"Converted {functions.Count} MCP tools to AI functions for agent {agentType}");
            }
            catch (Exception ex)
            {
                //logger.LogError(ex, "Failed to retrieve MCP tools for agent {AgentType}", agentType, ex);
            }
            
            return functions;
        }

        /// <summary>
        /// Create a dynamic method for the MCP tool with proper attributes
        /// </summary>
        private static System.Reflection.MethodInfo? CreateDynamicToolMethod(McpToolExecutor executor, McpTool mcpTool)
        {
            // For now, use the existing ExecuteAsync method
            // In a more advanced implementation, we could create dynamic methods with proper signatures
            var method = typeof(McpToolExecutor).GetMethod(nameof(McpToolExecutor.ExecuteAsync));
            
            // We could enhance this to create methods with proper parameter signatures based on the MCP tool schema
            return method;
        }

        /// <summary>
        /// Proper MCP tool executor that integrates with Microsoft.Extensions.AI.Agents
        /// </summary>
        private class McpToolExecutor
        {
            private readonly MCPToolService _mcpService;
            private readonly AgentType _agentType;
            private readonly McpTool _mcpTool;
            private readonly ILogger _logger;

            public McpToolExecutor(MCPToolService mcpService, AgentType agentType, McpTool mcpTool, ILogger logger)
            {
                _mcpService = mcpService;
                _agentType = agentType;
                _mcpTool = mcpTool;
                _logger = logger;
            }

            [Description("Execute MCP banking tool")]
            public async Task<string> ExecuteAsync(Dictionary<string, object>? arguments = null)
            {
                try
                {
                    _logger.LogInformation("Executing MCP tool {ToolName} for agent {AgentType}", _mcpTool.Name, _agentType);
                    
                    // Use the MCP service to execute the tool with proper MCP client patterns
                    var result = await _mcpService.CallToolAsync(_agentType, _mcpTool.Name, arguments);
                    
                    // Handle different result types appropriately
                    return result switch
                    {
                        string stringResult => stringResult,
                        null => "Tool execution completed successfully",
                        _ => System.Text.Json.JsonSerializer.Serialize(result, new System.Text.Json.JsonSerializerOptions { WriteIndented = true })
                    };
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error executing MCP tool {ToolName} for agent {AgentType}", _mcpTool.Name, _agentType);
                    return $"Error executing {_mcpTool.Name}: {ex.Message}";
                }
            }
        }

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
        private static IList<AIFunction>? GetAgentTools(AgentType agentType, MockBankingService bankService, ILoggerFactory AloggerFactory)
        {
             LoggerFactory loggerFactory=new LoggerFactory();
            //ILogger logger = loggerFactory.CreateLogger<AgentFrameworkService>();
            try
            {
                Console.WriteLine($"Creating tools for agent type: {agentType}");

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
                Console.WriteLine($"Created {toolsClass.GetType().Name} for agent type: {agentType}");

                // Log available methods with Description attributes
                var methods = toolsClass.GetType().GetMethods()
                    .Where(m => m.GetCustomAttributes(typeof(DescriptionAttribute), false).Length > 0);



                IList<AIFunction> functions = new List<AIFunction>();
                foreach (var method in methods)
                {
                    functions.Add(AIFunctionFactory.Create(method, toolsClass));
                    var description = method.GetCustomAttribute<DescriptionAttribute>()?.Description ?? "No description";
                    Console.WriteLine($"Agent {agentType} has method: '{method.Name}' - {description}");
                }

                Console.WriteLine($"Tool class created for agent type: {agentType}. Returning null due to delegate binding limitations.");

                return functions;
            }
            catch (Exception ex)
            {
                //logger.LogError(ex, "Error creating tools for agent type: {AgentType}", agentType);
                return null;
            }
        }
    }  
}