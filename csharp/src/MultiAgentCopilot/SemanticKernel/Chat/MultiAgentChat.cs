using Microsoft.SemanticKernel.Connectors.AzureOpenAI;
using Microsoft.SemanticKernel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.Agents.Chat;
using Microsoft.SemanticKernel.Agents.History;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.AzureOpenAI;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using MultiAgentChatAPI.Prompts;
using MultiAgentChatAPI.Plugins;
using MultiAgentChatAPI.Model;
using MultiAgentChatAPI.StructuredFormats;
using OpenAI.Chat;
using System.Text.Json;
using Microsoft.Extensions.Logging;

#pragma warning disable SKEXP0110 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
#pragma warning disable SKEXP0010 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
#pragma warning disable SKEXP0001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.

namespace MultiAgentChatAPI.Agents
{
    internal class MultiAgentChat
    {
        private ChatCompletionAgent BuildAgent(Kernel kernel, AgentType agentType)
        {
            ChatCompletionAgent agent = new ChatCompletionAgent
            {
                Name = PromptGenerator.GetAgentName(agentType),
                Instructions = $"""{PromptGenerator.GetAgentPrompts(agentType)}""",
                Kernel = PluginSelector.GetAgentKernel(kernel, agentType),
                Arguments = new KernelArguments(new AzureOpenAIPromptExecutionSettings() { FunctionChoiceBehavior = FunctionChoiceBehavior.Auto() })
            };

            return agent;
        }



       private OpenAIPromptExecutionSettings GetExecutionSettings(ChatResponseFormatBuilder.ChatResponseStratergy stratergyType)
        {

            ChatResponseFormat infoFormat;
            infoFormat = ChatResponseFormat.CreateJsonSchemaFormat(
            jsonSchemaFormatName: $"agent_result_{stratergyType.ToString()}",
            jsonSchema: BinaryData.FromString($"""
                {ChatResponseFormatBuilder.BuildFormat(stratergyType)}
                """));
            var executionSettings = new OpenAIPromptExecutionSettings
            {
                ResponseFormat = infoFormat
            };

            return executionSettings;
        }


        private KernelFunction GetStratergyFunction(ChatResponseFormatBuilder.ChatResponseStratergy stratergyType)
        {
            const string TerminationToken = "no";
            const string ContinuationToken = "yes";

            KernelFunction function =
                AgentGroupChat.CreatePromptFunctionForStrategy(
                    $$$"""
                    {{{PromptGenerator.GetStratergyPrompts(stratergyType)}}}
                    
                    RESPONSE:
                    {{$lastmessage}}
                    """,
                    safeParameterNames: "lastmessage");

            return function;

       }



        public AgentGroupChat BuildAgentGroupChat(Kernel kernel, ILogger loger)
        {           

            AgentGroupChat agentGroupChat = new AgentGroupChat();

            var chatModel = kernel.GetRequiredService<IChatCompletionService>();

            ChatHistoryTruncationReducer historyReducer = new(5);


            foreach (AgentType agentType in Enum.GetValues(typeof(AgentType)))
            {
                agentGroupChat.AddAgent(BuildAgent(kernel, agentType));
            }

            AgentGroupChatSettings ExecutionSettings = new AgentGroupChatSettings
            {
                SelectionStrategy =
                    new KernelFunctionSelectionStrategy(GetStratergyFunction(ChatResponseFormatBuilder.ChatResponseStratergy.Continuation), kernel)
                    {
                        Arguments = new KernelArguments(GetExecutionSettings(ChatResponseFormatBuilder.ChatResponseStratergy.Continuation)),
                        // Always start with the editor agent.
                        //InitialAgent = cordinatorAgent,//do not set else cordinator initates after each stateless call.
                        // Save tokens by only including the final few responses
                        HistoryReducer = historyReducer,
                        // The prompt variable name for the history argument.
                        HistoryVariableName = "lastmessage",
                        // Returns the entire result value as a string.
                        ResultParser = (result) =>
                        {
                            var ContinuationInfo = JsonSerializer.Deserialize<ContinuationInfo>(result.GetValue<string>());
                            loger.LogTrace($"SELECTION - Agent:{ContinuationInfo.AgentName}"); // provides visibility (can use logger)
                            loger.LogTrace($"SELECTION - Reason:{ContinuationInfo.Reason}"); // provides visibility (can use logger)
                            return ContinuationInfo.AgentName;
                        }
                    },
                TerminationStrategy =
                    new KernelFunctionTerminationStrategy(GetStratergyFunction(ChatResponseFormatBuilder.ChatResponseStratergy.Termination), kernel)
                    {
                        Arguments = new KernelArguments(GetExecutionSettings(ChatResponseFormatBuilder.ChatResponseStratergy.Termination)),
                        // Save tokens by only including the final response
                        HistoryReducer = historyReducer,
                        // The prompt variable name for the history argument.
                        HistoryVariableName = "lastmessage",
                        // Limit total number of turns
                        MaximumIterations = 8,
                        // user result parser to determine if the response is "yes"

                        ResultParser = (result) =>
                        {
                            var terminationInfo = JsonSerializer.Deserialize<TerminationInfo>(result.GetValue<string>());
                            loger.LogTrace($"TERMINATION - Continue:{terminationInfo.ShouldContinue}"); // provides visibility (can use logger)
                            loger.LogTrace($"TERMINATION - Reason:{terminationInfo.Reason}"); // provides visibility (can use logger)
                            return !terminationInfo.ShouldContinue;
                        }
                    },
            };

            return agentGroupChat;
        }
    }

    enum AgentType
    {
        Transactions=0,
        Sales=1,
        CustomerSupport=2,
        Cordinator=3,
    }
}
