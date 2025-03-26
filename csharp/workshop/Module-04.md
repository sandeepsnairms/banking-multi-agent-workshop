# Module 04 - Multi-Agent Orchestration

[< Agent Specialization](./Module-03.md) - **[Home](Home.md)** - [Lessons Learned, Agent Futures, Q&A >](./Module-05.md)

## Introduction

In this Module you'll learn how to implement the multi-agent orchestration to tie all of the agents you have created so far together into a single system. You'll also learn how to test the system as a whole is working correctly and how to debug and monitor the agents performance and behavior and troubleshoot them.

## Learning Objectives

- Learn how to write prompts for agents
- Define agent routing
- Learn how to define API contracts for a multi-agent system
- Learn how to test and debug agents, monitoring

## Module Exercises

1. [Activity 1: Session on Multi-Agent Architectures](#activity-1-session-on-multi-agent-architectures)
1. [Activity 2: Define Agents and Roles](#activity-2-define-agents-and-roles)
1. [Activity 3: Session on Testing and Monitoring](#activity-3-session-on-testing-and-monitoring)
1. [Activity 4: Implement Agent Tracing and Monitoring](#activity-4-implement-agent-tracing-and-monitoring)
1. [Activity 5: Test your Work](#activity-5-test-your-work)

## Activity 1: Session on Multi-Agent Architectures

In this session you will learn how this all comes together and get insights into how the multi-agent orchestration works and coordinates across all of the defined agents for your system.

## Activity 2: Define Agents and Roles

When dealing with multiple agents, clear agent roles is important to avoid conflicts and making sure  customer gets the most appropriate response.

### Add Selection Strategy for Agent Selection

SelectionStrategy is the mechanism in SemanticKernel that determines the next participant. By using the SelectionStrategy, we can identify the available agents and guide the LLM by defining the selection rule in natural language.

Add SelectionStrategy.prompty at ChatAPI\Prompts\

```
Examine RESPONSE and choose the next participant.

Choose only from these participants:
- Coordinator
- CustomerSupport
- Sales
- Transactions

Always follow these rules when choosing the next participant:
- Determine the nature of the user's request and route it to the appropriate agent
- If the user is responding to an agent, select that same agent.
- If the agent is responding after fetching or verifying data , select that same agent.
- If unclear, select Coordinator.

```

### Add Termination Strategy for Agent response

Similar to how SelectionStrategy selects an agent, TerminationStrategy decides when agents should stop responding. This is crucial to prevent multiple unwanted agent chatter in response to a user prompt. TerminationStrategy is the mechanism in SemanticKernel that determines when to stop. It is defined in natural language and instructs the LLM to return YES if more agent participation is required, otherwise it should return NO.

Add TerminationStrategy.prompty at ChatAPI\Prompts\

```
Determine if agent has requested user input or has responded to the user's query.
Respond with the word NO (without explanation) if agent has requested user input.
Otherwise, respond with the word YES (without explanation) if any the following conditions are met:
- An action is pending by an agent.
- Further participation from an agent is required
- The information requested by the user was not provided by the current agent.
```

### ChatResponseFormat

By default, the LLM responds to user prompts in natural language. However, we can enforce a structured format in its responses. A structured format allows us to parse the response and utilize it in our code for decision-making.

Lets define the models for the response

### Add ContinuationInfo.cs  to ChatInfrastructure\Models\ChatInfoFormats

```c#

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MultiAgentCopilot.ChatInfrastructure.Models.ChatInfoFormats
{
    public class ContinuationInfo
    {
        public string AgentName { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
    }
}
```

### Add TerminationInfo.cs  to ChatInfrastructure\Models\ChatInfoFormats

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MultiAgentCopilot.ChatInfrastructure.Models.ChatInfoFormats
{
    internal class TerminationInfo
    {
        public bool ShouldContinue { get; set; }
        public string Reason { get; set; } = string.Empty;
    }
}

```

Let's define a format builder that the LLM can use to output the Continuation and Termination models as responses.

Add ChatResponseFormat.cs to ChatInfrastructure\StructuredFormats

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace MultiAgentCopilot.ChatInfrastructure.StructuredFormats
{
    internal static class ChatResponseFormatBuilder
    {
        internal enum ChatResponseStrategy
        {
            Continuation,
            Termination

        }

        internal static string BuildFormat(ChatResponseStrategy strategyType)
        {
            switch (strategyType)
            {
                case ChatResponseStrategy.Continuation:
                    string jsonSchemaFormat_Continuation = """
                    {

                        "type": "object", 
                            "properties": {
                                "AgentName": { "type": "string", "description":"name of the selected agent" },
                                "Reason": { "type": "string","description":"reason for selecting the agent" }
                            },
                            "required": ["AgentName", "Reason"],
                            "additionalProperties": false

                    }
                    """;

                    return jsonSchemaFormat_Continuation;
                case ChatResponseStrategy.Termination:
                    string jsonSchemaFormat_termination = """
                    {

                        "type": "object", 
                            "properties": {
                                "ShouldContinue": { "type": "boolean", "description":"Does conversation require further agent participation" },
                                "Reason": { "type": "string","description":"List the conditions that evaluated to true for further agent participation" }
                            },
                            "required": ["ShouldContinue", "Reason"],
                            "additionalProperties": false

                    }
                    """;

                    return jsonSchemaFormat_termination;
                default:
                    return "";
            }

        }
    }


}


```

Add reference to MultiAgentCopilot.ChatInfrastructure.StructuredFormats.ChatResponseFormatBuilder in ChatInfrastructure\Factories\SystemPromptFactory.cs 

```csharp
using static MultiAgentCopilot.ChatInfrastructure.StructuredFormats.ChatResponseFormatBuilder;

```

### StrategyPrompts

Dynamically load the prompts based on strategyType

Add GetStrategyPrompts to ChatInfrastructure\Factories\SystemPromptFactory.cs 

```csharp

   public static string GetStrategyPrompts(ChatResponseStrategy strategyType)
   {
      string prompt = string.Empty;
      switch (strategyType)
      {
            case ChatResponseStrategy.Continuation:
               prompt = File.ReadAllText("Prompts/SelectionStrategy.prompty");
               break;
            case ChatResponseStrategy.Termination:
               prompt = File.ReadAllText("Prompts/TerminationStrategy.prompty");
               break;

      }
      return prompt;
   }

```

### Update Chat Factory to replace Agent with AgentGroupChat

Add the below references in ChatInfrastructure/Factories/ChatFactory.cs

```csharp

using MultiAgentCopilot.ChatInfrastructure.StructuredFormats;
using MultiAgentCopilot.ChatInfrastructure.Models.ChatInfoFormats;
using MultiAgentCopilot.ChatInfrastructure.Models;
using MultiAgentCopilot.ChatInfrastructure.Logs;


```


Add the functions in ChatInfrastructure\Factories\ChatFactory.cs

```csharp
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
                    {{{SystemPromptFactory.GetStrategyPrompts(strategyType)}}}
                    
                    RESPONSE:
                    {{$lastmessage}}
                    """,
                    safeParameterNames: "lastmessage");

            return function;
        }
               

        public AgentGroupChat BuildAgentGroupChat(Kernel kernel, ILoggerFactory loggerFactory, LogCallback logCallback, IBankDataService bankService, string tenantId, string userId)
        {
            AgentGroupChat agentGroupChat = new AgentGroupChat();
            var chatModel = kernel.GetRequiredService<IChatCompletionService>();

            //kernel.AutoFunctionInvocationFilters.Add(new AutoFunctionInvocationLoggingFilter(loggerFactory.CreateLogger<AutoFunctionInvocationLoggingFilter>()));

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
        }

```

### Replace Agent with AgentGroupChat in SemanticKernel

Till now the responses we received were from a single agent, lets us use AgentGroupChat to orchestrate a chat were multiple agents participate.

Update GetResponse in ChatInfrastructure\Services\SemanticKernelService.cs

```csharp

 public async Task<Tuple<List<Message>, List<DebugLog>>> GetResponse(Message userMessage, List<Message> messageHistory, IBankDataService bankService, string tenantId, string userId)
    {
        try
        {
            ChatFactory multiAgentChatGeneratorService = new ChatFactory();

            var agentGroupChat = multiAgentChatGeneratorService.BuildAgentGroupChat(_semanticKernel, _loggerFactory, LogMessage, bankService, tenantId, userId);

            // Load history
            foreach (var chatMessage in messageHistory)
            {
                AuthorRole? role = AuthorRoleHelper.FromString(chatMessage.SenderRole);
                var chatMessageContent = new ChatMessageContent
                {
                    Role = role ?? AuthorRole.User,
                    Content = chatMessage.Text
                };
                agentGroupChat.AddChatMessage(chatMessageContent);
            }

            _promptDebugProperties = new List<LogProperty>();

            List<Message> completionMessages = new();
            List<DebugLog> completionMessagesLogs = new();
            do
            {
                var userResponse = new ChatMessageContent(AuthorRole.User, userMessage.Text);
                agentGroupChat.AddChatMessage(userResponse);

                agentGroupChat.IsComplete = false;

                await foreach (ChatMessageContent response in agentGroupChat.InvokeAsync())
                {
                    string messageId = Guid.NewGuid().ToString();

                    completionMessages.Add(new Message(userMessage.TenantId, userMessage.UserId, userMessage.SessionId, response.AuthorName ?? string.Empty, response.Role.ToString(), response.Content ?? string.Empty, messageId));

                }
            }
            while (!agentGroupChat.IsComplete);


            return new Tuple<List<Message>, List<DebugLog>>(completionMessages, completionMessagesLogs);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error when getting response: {ErrorMessage}", ex.ToString());
            return new Tuple<List<Message>, List<DebugLog>>(new List<Message>(), new List<DebugLog>());
        }
    }

```

## Activity 3: Session on Testing and Monitoring

In this session you will learn about how to architect the service layer for a multi-agent system and how to configure and conduct testing and debugging and monitoring for these systems.

## Activity 4: Implement Agent Tracing and Monitoring

In this hands-on exercise, you will learn how to define an API service layer for a multi-agent backend and learn how to configure tracing and monitoring to enable testing and debugging for agents.

Before executing the below steps, try chatting with the agents. Note that  you are unable to see what what the LLM select an agent. Now lets add some code to bring visibility to behind the scene decision making process.

### Log the kernel function selection

To log the data used by the LLM to invoke functions, we will create a class named `AutoFunctionInvocationLoggingFilter`. Add AutoFunctionInvocationLoggingFilter.cs to ChatInfrastructure\Logs.

```c#

using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace MultiAgentCopilot.ChatInfrastructure.Logs
{
    public sealed class AutoFunctionInvocationLoggingFilter : IAutoFunctionInvocationFilter
    {
        private readonly ILogger<AutoFunctionInvocationLoggingFilter> _logger;

        public AutoFunctionInvocationLoggingFilter(ILogger<AutoFunctionInvocationLoggingFilter> logger)
        {
            _logger = logger;

        }

        public async Task OnAutoFunctionInvocationAsync(AutoFunctionInvocationContext context, Func<AutoFunctionInvocationContext, Task> next)
        {

            var functionCalls = FunctionCallContent.GetFunctionCalls(context.ChatHistory.Last()).ToList();

            if (_logger.IsEnabled(LogLevel.Trace))
            {
                functionCalls.ForEach(functionCall
                    => _logger.LogTrace(
                        "Function call requests: {PluginName}-{FunctionName}({Arguments})",
                        functionCall.PluginName,
                        functionCall.FunctionName,
                        JsonSerializer.Serialize(functionCall.Arguments)));
            }

            await next(context);
        }
    }
}
```

To start logging update kernel's AutoFunctionInvocationFilters by uncommenting the following line in the `BuildAgentGroupChat` function in ChatFactory.cs.

```csharp

    //kernel.AutoFunctionInvocationFilters.Add(new AutoFunctionInvocationLoggingFilter(loggerFactory.CreateLogger<AutoFunctionInvocationLoggingFilter>()));

```

### Log the KernelFunctionSelectionStrategy and KernelFunctionTerminationStrategy

Update GetAgentGroupChatSettings in ChatInfrastructure\Factories\ChatFactory.cs

```csharp

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

```

### Store the log information along with the chat response.

Update GetResponse in ChatInfrastructure\Services\SemanticKernelService.cs

```csharp

 public async Task<Tuple<List<Message>, List<DebugLog>>> GetResponse(Message userMessage, List<Message> messageHistory, IBankDataService bankService, string tenantId, string userId)
    {
        try
        {
            ChatFactory multiAgentChatGeneratorService = new ChatFactory();

            var agentGroupChat = multiAgentChatGeneratorService.BuildAgentGroupChat(_semanticKernel, _loggerFactory, LogMessage, bankService, tenantId, userId);

            // Load history
            foreach (var chatMessage in messageHistory)
            {
                AuthorRole? role = AuthorRoleHelper.FromString(chatMessage.SenderRole);
                var chatMessageContent = new ChatMessageContent
                {
                    Role = role ?? AuthorRole.User,
                    Content = chatMessage.Text
                };
                agentGroupChat.AddChatMessage(chatMessageContent);
            }

            _promptDebugProperties = new List<LogProperty>();

            List<Message> completionMessages = new();
            List<DebugLog> completionMessagesLogs = new();
            do
            {
                var userResponse = new ChatMessageContent(AuthorRole.User, userMessage.Text);
                agentGroupChat.AddChatMessage(userResponse);

                agentGroupChat.IsComplete = false;

                await foreach (ChatMessageContent response in agentGroupChat.InvokeAsync())
                {
                    string messageId = Guid.NewGuid().ToString();
                    string debugLogId = Guid.NewGuid().ToString();
                    completionMessages.Add(new Message(userMessage.TenantId, userMessage.UserId, userMessage.SessionId, response.AuthorName ?? string.Empty, response.Role.ToString(), response.Content ?? string.Empty, messageId, debugLogId));

                    if (_promptDebugProperties.Count > 0)
                    {
                        var completionMessagesLog = new DebugLog(userMessage.TenantId, userMessage.UserId, userMessage.SessionId, messageId, debugLogId);
                        completionMessagesLog.PropertyBag = _promptDebugProperties;
                        completionMessagesLogs.Add(completionMessagesLog);
                    }

                }
            }
            while (!agentGroupChat.IsComplete);

            return new Tuple<List<Message>, List<DebugLog>>(completionMessages, completionMessagesLogs);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error when getting response: {ErrorMessage}", ex.ToString());
            return new Tuple<List<Message>, List<DebugLog>>(new List<Message>(), new List<DebugLog>());
        }
    }

```

## Activity 5: Test your Work

With the hands-on exercises complete it is time to test your work.

1. Navigate to src\ChatAPI.
    - If running on Codespaces:
       1. Run dotnet dev-certs https --trust to manually accept the certificate warning.
       2. Run dotnet run.
    - If running locally on Visual Studio or VS Code:
       1. Press F5 or select Run.
2. Copy the launched URL and use it as the API endpoint in the next step.
3. Follow the [instructions](../..//README.md) to run the Frontend app.
4. Start a conversation in the UI
5. Try the below messages and respond according to the AI response.
    1. Transfer $50 to my friend.
    1. Looking for a Savings account with high interest rate.
    1. File a complaint about theft from my account.
    1. How much did I spend on grocery?
    1. Provide me a statement of my account.
1. Expected result each message response is based on the agent you selected and plugins available with the agent.

### Validation Checklist

- [ ] item 1
- [ ] item 2
- [ ] item 3

### Common Issues and Solutions

1. Item 1:

   - Sub item 1
   - Sub item 2
   - Sub item 3

1. Item 2:

   - Sub item 1
   - Sub item 2
   - Sub item 3

3. Item 3:

   - Sub item 1
   - Sub item 2
   - Sub item 3

### Module Solution

<details>
  <summary>If you are encounting errors or issues with your code for this module, please refer to the following code.</summary>

<br>

Explanation for code and where it goes. Multiple sections of these if necessary.
```python
# Your code goes here

```
</details>

## Next Steps

Proceed to [Lessons Learned, Agent Futures, Q&A](./Module-04.md)

## Resources

- [Semantic Kernel Agent Framework](https://learn.microsoft.com/semantic-kernel/frameworks/agent)
- [LangGraph](https://langchain-ai.github.io/langgraph/concepts/)
- [Azure OpenAI Service documentation](https://learn.microsoft.com/azure/cognitive-services/openai/)
- [Azure Cosmos DB Vector Database](https://learn.microsoft.com/azure/cosmos-db/vector-database)

