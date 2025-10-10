using Azure.Core;
using Banking.Services;
using Microsoft.Agents.AI;
using Microsoft.AspNetCore.Components.Routing;
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
    /// <summary>
    /// Diagnostics information for MCP integration validation
    /// </summary>
    public class AgentMCPDiagnostics
    {
        public AgentType AgentType { get; set; }
        public string AgentName { get; set; } = string.Empty;
        public bool IsConfigured { get; set; }
        public bool IsConnected { get; set; }
        public string? ServerUrl { get; set; }
        public int ToolCount { get; set; }
        public List<string> Tools { get; set; } = new();
        public string? ErrorMessage { get; set; }
    }

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
                logger.LogInformation("Created agent {AgentName} with {ToolCount} InProcess", agent.Name, aiFunctions.Count());
            }

            logger.LogInformation("Successfully created {AgentCount} banking agents", agents.Count);
            return agents;
        }

        public static async Task<List<AIAgent>> CreateAllAgentsWithMCPToolsAsync(IChatClient chatClient, MCPToolService mcpService, ILoggerFactory loggerFactory)
        {
            //var aiFunctions2 = await mcpService.GetMcpTools(AgentType.Coordinator);
            //var agent2 = chatClient.CreateAIAgent(
            //            instructions: "Your responsibility is to identify the currently logged -in user's information and welcome the user using full name.",
            //            name: "Welcome",
            //            description: "Welcome agent",
            //            tools: aiFunctions2.ToArray()
            //        );

            //List<Microsoft.Extensions.AI.ChatMessage> history = new List<Microsoft.Extensions.AI.ChatMessage>();
            //history.Add(new Microsoft.Extensions.AI.ChatMessage(Microsoft.Extensions.AI.ChatRole.User, "Hi, I just logged in. User: Mark, Tenant: Contoso"));

            //var response2 = agent2.RunAsync(history).GetAwaiter().GetResult();


            var agents = new List<AIAgent>();
            ILogger logger = loggerFactory.CreateLogger("AgentFactory");

            // Get all agent types from the enum
            var agentTypes = Enum.GetValues<AgentType>();

            // Create agents for each agent type
            foreach (var agentType in agentTypes)
            {
                logger.LogInformation("Creating agent {AgentType} with MCP tools", agentType);

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
                logger.LogInformation("Creating in-process tools for agent type: {AgentType}", agentType);

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

                // Get methods with Description attributes and create AI functions
                var methods = toolsClass.GetType().GetMethods()
                    .Where(m => m.GetCustomAttributes(typeof(DescriptionAttribute), false).Length > 0);

                IList<AIFunction> functions = new List<AIFunction>();
                
                foreach (var method in methods)
                {
                    try
                    {
                        var aiFunction = AIFunctionFactory.Create(method, toolsClass);
                        functions.Add(aiFunction);
                        
                        var description = method.GetCustomAttribute<DescriptionAttribute>().Description;
                        logger.LogDebug("Agent {AgentType} in-process tool: '{MethodName}' - {Description}",
                            agentType, method.Name, description);
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(ex, "Failed to create AI function for method {MethodName} in {AgentType}: {Message}",
                            method.Name, agentType, ex.Message);
                    }
                }

                logger.LogInformation("Created {FunctionCount} in-process tools for agent type: {AgentType}", 
                    functions.Count, agentType);

                return functions.Count > 0 ? functions : null;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error creating in-process tools for agent type: {AgentType}", agentType);
                return null;
            }
        }

        
    }
}