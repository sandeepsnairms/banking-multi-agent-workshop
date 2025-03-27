using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.Agents.Chat;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.SemanticKernel.Connectors.AzureOpenAI;
using MultiAgentCopilot.ChatInfrastructure.Helper;
using MultiAgentCopilot.ChatInfrastructure.Models;
using BankingServices.Interfaces;
using OpenAI.Chat;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using System.Reflection.Metadata;

namespace MultiAgentCopilot.ChatInfrastructure.Factories
{
    internal class ChatFactory
    {
        public delegate void LogCallback(string key, string value);

        public ChatCompletionAgent BuildAgent(Kernel kernel, AgentType agentType, ILoggerFactory loggerFactory, IBankDataService bankService, string tenantId, string userId)
        {
            ChatCompletionAgent agent = new ChatCompletionAgent
            {
                Name = SystemPromptFactory.GetAgentName(agentType),
                Instructions = $"""{SystemPromptFactory.GetAgentPrompts(agentType)}""",
                Kernel = PluginFactory.GetAgentKernel(kernel, agentType, loggerFactory, bankService, tenantId, userId),
                Arguments = new KernelArguments(new AzureOpenAIPromptExecutionSettings() { FunctionChoiceBehavior = FunctionChoiceBehavior.Auto() })
            };

            return agent;
        }
    }
}