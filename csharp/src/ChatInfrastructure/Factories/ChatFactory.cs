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



using OpenAI.Chat;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using MultiAgentCopilot.ChatInfrastructure.Logs;

using System.Reflection.Metadata;


namespace MultiAgentCopilot.ChatInfrastructure.Factories
{
    internal class ChatFactory
    {
        public delegate void LogCallback(string key, string value);

        public ChatCompletionAgent BuildAgent(Kernel kernel, ILoggerFactory loggerFactory, string tenantId, string userId)
        {
            ChatCompletionAgent agent = new ChatCompletionAgent
            {
                Name = SystemPromptFactory.GetAgentName(),
                Instructions = $"""{SystemPromptFactory.GetAgentPrompts()}""",
                Kernel = kernel.Clone()
            };

            return agent;
        }       

    }
}
