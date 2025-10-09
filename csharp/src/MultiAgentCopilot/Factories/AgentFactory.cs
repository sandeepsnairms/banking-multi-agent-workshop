using Azure.Core;
using Banking.Services;
using Microsoft.Agents.AI;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.Exchange.WebServices.Data;
using Microsoft.Extensions.AI;
using MultiAgentCopilot.Models;
using MultiAgentCopilot.MultiAgentCopilot.Services;
using MultiAgentCopilot.Services;
using MultiAgentCopilot.Tools;
using OpenAI;
using OpenAI.Chat;
using OpenAI.Responses;
using System;
using System.Buffers.Text;
using System.ComponentModel;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;

namespace MultiAgentCopilot.Factories
{
    public static class AgentFactory
    {

        /// <summary>
        /// Create all banking agents with proper instructions and tools
        /// </summary>
        public static List<AIAgent> CreateAllAgentsWithInProcessTools(IChatClient chatClient, BankingDataService bankService, ILoggerFactory loggerFactory)
        {
            //var aiFunctions2 = GetInProcessAgentTools(AgentType.Coordinator, bankService, loggerFactory).ToArray();
            //var agent2 = chatClient.CreateAIAgent(
            //            instructions: "Your primary responsibilities include welcoming users, identifying customers based on their login.Start with identifying the currently logged -in user's information and use it to personalize the interaction.For example, Thank you for logging in, [user Name]. How can I help you with your banking needs today?",
            //            name: "Welcome",
            //            description: "Welcome agent",
            //            tools: aiFunctions2.ToArray()
            //        );

            //List<Microsoft.Extensions.AI.ChatMessage> history=new List<Microsoft.Extensions.AI.ChatMessage>();
            //history.Add(new Microsoft.Extensions.AI.ChatMessage(Microsoft.Extensions.AI.ChatRole.User, "Hi, I just logged in. User: Mark, Tenant: Contoso"));

            //var response2 = agent2.RunAsync(history).GetAwaiter().GetResult();

            var agents = new List<AIAgent>();
            ILogger logger = loggerFactory.CreateLogger("AgentFactory");

            // Get all agent types from the enum
            var agentTypes = Enum.GetValues<AgentType>();

            // Create agents for each agent type
            foreach (var agentType in agentTypes)
            {
                logger.LogInformation("Creating agent {AgentType} with InProcess tools", agentType);

                var aiFunctions = GetInProcessAgentTools(agentType, bankService, loggerFactory).ToArray();

                var agent = chatClient.CreateAIAgent(
                        instructions: GetAgentPrompt(agentType),
                        name: GetAgentName(agentType),
                        description: GetAgentDescription(agentType),
                        tools: aiFunctions.ToArray()
                    );

                agents.Add(agent);
                logger.LogInformation("Created agent {AgentName} with {ToolCount} MCP tools", agent.Name, aiFunctions.Count());
            }

            logger.LogInformation("Successfully created {AgentCount} banking agents", agents.Count);
            return agents;
        }

        public static async Task<List<AIAgent>> CreateAllAgentsWithMCPToolsAsync(IChatClient chatClient, MCPToolService mcpService, ILoggerFactory loggerFactory)
        {
            //var aiFunctions2 = await mcpService.GetMcpTools(AgentType.Coordinator);
            //var agent2 = chatClient.CreateAIAgent(
            //            instructions: "Your primary responsibilities include welcoming users, identifying customers based on their login.Start with identifying the currently logged -in user's information and use it to personalize the interaction.For example, Thank you for logging in, [user Name]. How can I help you with your banking needs today?",
            //            name: "Welcome",
            //            description: "Welcome agent",
            //            tools: aiFunctions2.ToArray()
            //        );

            //List<Microsoft.Extensions.AI.ChatMessage> history = new List<Microsoft.Extensions.AI.ChatMessage>();
            //history.Add(new Microsoft.Extensions.AI.ChatMessage(Microsoft.Extensions.AI.ChatRole.User, "Hi, I just logged in. User: Mark, Tenant: Contoso"));

            //var response2 = agent2.RunAsync(history).GetAwaiter().GetResult();


            var agents = new List<Microsoft.Agents.AI.AIAgent>();
            ILogger logger = loggerFactory.CreateLogger("AgentFactory");

            // Get all agent types from the enum
            var agentTypes = Enum.GetValues<AgentType>();

            // Create agents for each agent type with MCP integration
            foreach (var agentType in agentTypes)
            {
                try
                {
                    logger.LogInformation("Creating agent {AgentType} with MCP tools", agentType);

                    // Convert MCP tools to AI functions using proper async MCP client patterns
                    var aiFunctions = await mcpService.GetMcpTools(agentType);

                    var agent = chatClient.CreateAIAgent(                        
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
        private static IList<AIFunction>? GetInProcessAgentTools(AgentType agentType, BankingDataService bankService, ILoggerFactory loggerFactory)
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