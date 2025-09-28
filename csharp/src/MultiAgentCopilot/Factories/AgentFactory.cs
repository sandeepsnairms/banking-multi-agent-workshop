using Microsoft.Extensions.AI;
using Microsoft.Extensions.AI.Agents;
using MultiAgentCopilot.Models;
using BankingModels;
using MultiAgentCopilot.MultiAgentCopilot.Services;
using MultiAgentCopilot.Services;
using MultiAgentCopilot.Tools;
using OpenAI.Chat;
using System.ComponentModel;
using System.Reflection;
using System.Text.Json;

namespace MultiAgentCopilot.Factories
{
    public static class AgentFactory
    {
        private static ChatClientAgent CreateAgent(Microsoft.Extensions.AI.IChatClient chatClient, ILoggerFactory loggerFactory, string instructions, string? description = null, string? name = null, params AIFunction[] functions)
        {

            // Correct constructor usage for ChatClientAgent:
            var options = new ChatClientAgentOptions
            {
                Instructions = instructions,
                Name = name,
                Description = description,
                ChatOptions = new() { Tools = functions, ToolMode = ChatToolMode.Auto }
            };
            return new ChatClientAgent(chatClient, options, loggerFactory);
        }


        /// <summary>
        /// Create all banking agents with proper instructions and tools
        /// </summary>
        public static List<AIAgent> CreateAllAgents(Microsoft.Extensions.AI.IChatClient chatClient, BankingDataService bankService, string tenantId, string userId, ILoggerFactory loggerFactory)
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
                    functions: GetAgentTools(agentType, bankService, tenantId, userId, loggerFactory).ToArray()
                );

                agents.Add(agent);
                logger.LogInformation($"Created {agent.Name}: {agent.Description}");
            }

            logger.LogInformation("Successfully created {AgentCount} banking agents", agents.Count);
            return agents;
        }


        public static List<AIAgent> CreateAllAgents(Microsoft.Extensions.AI.IChatClient chatClient, MCPToolService mcpService, string tenantId, string userId, ILoggerFactory loggerFactory)
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
                    functions: mcpService.GetMcpTools(agentType).GetAwaiter().GetResult().ToArray()
                );

                agents.Add(agent);
                logger.LogInformation($"Created {agent.Name}: {agent.Description}");
            }

            logger.LogInformation("Successfully created {AgentCount} banking agents", agents.Count);
            return agents;
        }

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
        private static IList<AIFunction>? GetAgentTools(AgentType agentType, BankingDataService bankService, string tenantId, string userId, ILoggerFactory loggerFactory)
        {
            ILogger logger = loggerFactory.CreateLogger<AgentFrameworkService>();
            try
            {
                logger.LogInformation("Creating tools for agent type: {AgentType}", agentType);

                // Create the appropriate tools class based on agent type
                BaseTools toolsClass = agentType switch
                {
                    AgentType.Sales => new SalesTools(loggerFactory.CreateLogger<SalesTools>(), bankService, tenantId, userId),
                    AgentType.Transactions => new TransactionTools(loggerFactory.CreateLogger<TransactionTools>(), bankService, tenantId, userId),
                    AgentType.CustomerSupport => new CustomerSupportTools(loggerFactory.CreateLogger<CustomerSupportTools>(), bankService, tenantId, userId),
                    AgentType.Coordinator => new CoordinatorTools(loggerFactory.CreateLogger<CoordinatorTools>(), bankService, tenantId, userId),
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