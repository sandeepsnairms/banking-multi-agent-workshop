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
using MultiAgentCopilot.StructuredFormats;
using MultiAgentCopilot.Models.ChatInfoFormats;

using OpenAI.Chat;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using MultiAgentCopilot.Logs;
using MultiAgentCopilot.Models;
using Microsoft.SemanticKernel.Connectors.Ollama;
using Microsoft.Identity.Client;
using Newtonsoft.Json.Schema;
using MultiAgentCopilot.Services;


namespace MultiAgentCopilot.Factories
{
    internal class ChatFactory
    {
        public delegate void LogCallback(string key, string value);

        private ChatCompletionAgent BuildAgent(Kernel kernel, AgentType agentType, ILoggerFactory loggerFactory, BankingDataService bankService, string tenantId, string userId)
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

        /*
#pragma warning disable SKEXP0070 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
        private OllamaPromptExecutionSettings GetExecutionSettings(ChatResponseFormatBuilder.ChatResponseStrategy strategyType)
        {

            var prompt = "Always responds in JSON format as defined in the ResponseFormat";
            var jsonSchema = ChatResponseFormatBuilder.BuildFormat(strategyType);
            // Create the execution settings
            var executionSettings = new OllamaPromptExecutionSettings
            {
                ExtensionData = new Dictionary<string, object>
                    {
                        { "Prompt", prompt },
                        { "ResponseFormat", jsonSchema }
                    }
            };



            return executionSettings;
        }
        */

        
        private OpenAIPromptExecutionSettings GetExecutionSettings(ChatResponseFormatBuilder.ChatResponseStrategy strategyType)
        {
            ChatResponseFormat infoFormat;
            infoFormat = ChatResponseFormat.CreateJsonSchemaFormat(
            jsonSchemaFormatName: $"agent_result_{strategyType.ToString()}",
            jsonSchema: BinaryData.FromString($"""
                {ChatResponseFormatBuilder.BuildFormat(strategyType)}
                """));
            var executionSettings = new OpenAIPromptExecutionSettings
            {
                ResponseFormat = infoFormat
            };

            return executionSettings;
        }
        
        private KernelFunction GetStrategyFunction(ChatResponseFormatBuilder.ChatResponseStrategy strategyType)
        {

            KernelFunction function =
                AgentGroupChat.CreatePromptFunctionForStrategy(
                    $$$"""
                    {{{SystemPromptFactory.GetStratergyPrompts(strategyType)}}}
                    
                    RESPONSE:
                    {{$lastmessage}}
                    """,
                    safeParameterNames: "lastmessage");

            return function;
        }
               

        public AgentGroupChat BuildAgentGroupChat(Kernel kernel, ILoggerFactory loggerFactory, LogCallback logCallback, BankingDataService bankService, string tenantId, string userId)
        {
            AgentGroupChat agentGroupChat = new AgentGroupChat();
            var chatModel = kernel.GetRequiredService<IChatCompletionService>();

            kernel.AutoFunctionInvocationFilters.Add(new AutoFunctionInvocationLoggingFilter(loggerFactory.CreateLogger<AutoFunctionInvocationLoggingFilter>()));

            foreach (AgentType agentType in Enum.GetValues(typeof(AgentType)))
            {
                agentGroupChat.AddAgent(BuildAgent(kernel, agentType, loggerFactory, bankService, tenantId, userId));
            }

            agentGroupChat.ExecutionSettings = GetAgentGroupChatSettings(kernel, logCallback);


            return agentGroupChat;
        }


        private AgentGroupChatSettings GetAgentGroupChatSettings(Kernel kernel, LogCallback logCallback)
        {
            ChatHistoryTruncationReducer historyReducer = new(5);

            AgentGroupChatSettings ExecutionSettings = new AgentGroupChatSettings
            {
                SelectionStrategy =
                    new KernelFunctionSelectionStrategy(GetStrategyFunction(ChatResponseFormatBuilder.ChatResponseStrategy.Continuation), kernel)
                    {
                        Arguments = new KernelArguments(GetExecutionSettings(ChatResponseFormatBuilder.ChatResponseStrategy.Continuation)),
                        // Save tokens by only including the final few responses
                        HistoryReducer = historyReducer,
                        // The prompt variable name for the history argument.
                        HistoryVariableName = "lastmessage",
                        // Returns the entire result value as a string.
                        ResultParser = (result) =>
                        {
                            var resultString = result.GetValue<string>();
                            if (!string.IsNullOrEmpty(resultString))
                            {
                                var ContinuationInfo = JsonSerializer.Deserialize<ContinuationInfo>(resultString);
                                logCallback("SELECTION - Agent", ContinuationInfo.AgentName); 
                                logCallback("SELECTION - Reason", ContinuationInfo.Reason);                       
                                return ContinuationInfo.AgentName;
                            }
                            else
                            {
                                return string.Empty;
                            }
                        }
                    },
                TerminationStrategy =
                    new KernelFunctionTerminationStrategy(GetStrategyFunction(ChatResponseFormatBuilder.ChatResponseStrategy.Termination), kernel)
                    {
                        Arguments = new KernelArguments(GetExecutionSettings(ChatResponseFormatBuilder.ChatResponseStrategy.Termination)),
                        // Save tokens by only including the final response
                        HistoryReducer = historyReducer,
                        // The prompt variable name for the history argument.
                        HistoryVariableName = "lastmessage",
                        // Limit total number of turns
                        MaximumIterations = 8,
                        // user result parser to determine if the response is "yes"
                        ResultParser = (result) =>
                        {
                            var resultString = result.GetValue<string>();
                            if (!string.IsNullOrEmpty(resultString))
                            {
                                var terminationInfo = JsonSerializer.Deserialize<TerminationInfo>(resultString);
                                logCallback("TERMINATION - Continue", terminationInfo.ShouldContinue.ToString()); 
                                logCallback("TERMINATION - Reason", terminationInfo.Reason); 
                                return !terminationInfo.ShouldContinue;
                            }
                            else
                            {
                                return false;
                            }
                        }
                    },
            };

            return ExecutionSettings;
        }
    }
}
