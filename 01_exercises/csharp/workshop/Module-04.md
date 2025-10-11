# Module 04 - Multi-Agent Orchestration

## Introduction

In this module, you'll learn how to implement multi-agent orchestration to coordinate multiple specialized agents within a single system. You'll also learn how to test the system as a whole, debug agent interactions, and monitor agent performance and behavior.

## Learning Objectives

- Understand multi-agent coordination patterns and orchestration strategies
- Learn how to implement agent routing and selection logic
- Define communication protocols and handoff mechanisms between agents
- Implement monitoring, debugging, and tracing capabilities for multi-agent systems

## Module Exercises

1. [Activity 1: Define Agents and Roles](#activity-1-define-agents-and-roles)
1. [Optional Activity 2: Implement Agent Tracing and Monitoring](#activity-2-implement-agent-tracing-and-monitoring)
1. [Activity 3: Test your Work](#activity-3-test-your-work)

## Activity 1: Define Agents and Roles

When dealing with multiple agents, clear agent roles is important to avoid conflicts and making sure  customer gets the most appropriate response.

### Add Selection Strategy for Agent Selection

SelectionStrategy is the mechanism in Microsoft Agent Framework that determines the next participant in a multi-agent conversation. By using the SelectionStrategy, we can identify the available agents and guide the LLM by defining the selection rule in natural language.

1. In VS Code, navigate to the **/Prompts** folder
1. Review the contents of **SelectionStrategy.prompty**

This prompt template guides the LLM in selecting the most appropriate agent. It provides clear routing rules based on the conversation context and user intent.

```text
You are mediator that guides a discussion. The current discussion is as follows: {discussion} 
You need to select the next participant to speak. 
Examine RESPONSE and choose the next participant.

Choose only from these participants:{participants}

Always follow these rules when choosing the next participant:
- Determine the nature of the user's request and route it to the appropriate agent
- If the user is responding to an agent, select that same agent.
- If the agent is responding after fetching or verifying data , select that same agent.
- If unclear, select Coordinator.
                    
Please respond with only the name of the participant you would like to select.

```

### Add Termination Strategy for Agent response

Similar to how SelectionStrategy selects an agent, TerminationStrategy decides when agents should stop responding. This is crucial to prevent multiple unwanted agent responses in multi-agent conversations. TerminationStrategy is the mechanism in Microsoft Agent Framework that determines when to stop the conversation loop.

1. In VS Code, navigate to the **/Prompts** folder.
1. Review the contents of **TerminationStrategy.prompty**

This prompt template helps the LLM determine when a conversation should end. It identifies when user input is needed or when the query has been fully addressed.

```text
Determine if agent has requested user input or has responded to the user's query based on the following conversation:

{topic}

Respond with a JSON object indicating whether the conversation should continue.
Set "ShouldContinue" to false (conversation should terminate) if agent has requested user input.
Set "ShouldContinue" to true (conversation should continue) if any of the following conditions are met:
- An action is pending by an agent.
- Further participation from an agent is required
- The information requested by the user was not provided by the current agent.

Also provide a "Reason" explaining your decision.
```

### ChatResponseFormat

By default, the LLM responds to user prompts in natural language. However, we can enforce a structured format in its responses. A structured format allows us to parse the response and utilize it in our code for decision-making.

Let's define the models for the response.

1. In VS Code, navigate to the **/Models/ChatInfoFormats** folder.
1. Review the contents of **ContinuationInfo.cs**.

This model defines the structure for agent selection responses. It includes the selected agent name and the reasoning behind the selection.

```c#
namespace MultiAgentCopilot.Models.ChatInfoFormats
{
    public class ContinuationInfo
    {
        public string AgentName { get; set; }
        public string Reason { get; set; }
    }
   
}
```

1. Remain in the same **ChatInfoFormats** folder.
1. Review the contents of **TerminationInfo.cs**

This model defines the structure for termination decision responses. It includes a boolean flag and reasoning for whether the conversation should continue.

```csharp
namespace MultiAgentCopilot.Models.ChatInfoFormats
{
    public class TerminationInfo
    {
        public bool ShouldContinue { get; set; }
        public string Reason { get; set; }
    }
}
```

Let's create an Agent to decide the next agent in the conversation, using the ContinuationInfo model as the response format.

1. In VS Code, navigate to the **/Helper** folder.
1. Replace the **SelectNextAgentAsync()** with the below code.

This method implements the agent selection logic using a moderator agent. It analyzes conversation history and selects the most appropriate next agent.

```csharp
 protected override async ValueTask<AIAgent> SelectNextAgentAsync(IReadOnlyList<ChatMessage> history, CancellationToken cancellationToken = default(CancellationToken))
 {
     // Convert chat history to a string representation for the prompt
     var historyText = string.Join("\n", history.TakeLast(5).Select(msg => 
     {
         var role = msg.Role.ToString();
         var content = msg.Text ?? "";
         return $"{role}: {content}";
     }));
     
     // Create a moderator agent to decide which agent should respond next
     ChatClientAgentOptions agentOptions = new(name: "Moderator", instructions: PromptFactory.Selection(historyText, GetAgentNames()))
     {
         ChatOptions = new()
         {
             ResponseFormat = Microsoft.Extensions.AI.ChatResponseFormat.ForJsonSchema(
                 schema: AIJsonUtilities.CreateJsonSchema(typeof(ContinuationInfo)),
                 schemaName: "ContinuationInfo",
                 schemaDescription: "Information about selecting next agent in a conversation.")
         }
     };

     var moderatorAgent = _chatClient.CreateAIAgent(agentOptions);

     // Get the selection recommendation from the moderator
     var response = await moderatorAgent.RunAsync(history);
     var selectionInfo = response.Deserialize<ContinuationInfo>(JsonSerializerOptions.Web);

     var selectedAgentName = selectionInfo?.AgentName?.ToString();
     var reason = selectionInfo?.Reason;

     // Log the selection decision (uncomment if you have logging)
     _logCallback?.Invoke("SelectNextAgentAsync", $"{{Agent: {selectedAgentName}, Reason: {reason}}}");

     // Find the matching agent from your agents list
     var selectedAgent = _agents.FirstOrDefault(agent =>
         string.Equals(agent.Name, selectedAgentName, StringComparison.OrdinalIgnoreCase));

     // Return the selected agent, or default to the first agent if no match found
     return selectedAgent ?? _agents[0];
     
 }
```
### Termination Decider

1. In VS Code, stay to the **/Helper** folder.
1. Replace the **ShouldTerminateWithAI()** with the below code.

```csharp
 private async Task<bool> ShouldTerminateWithAI(IReadOnlyList<ChatMessage> history, CancellationToken cancellationToken)
 {
     if (history == null || !history.Any())
         return false;

     // Convert chat history to a string representation for the prompt
     var historyText = string.Join("\n", history.TakeLast(10).Select(msg =>
     {
         var role = msg.Role.ToString();
         var content = msg.Text ?? "";
         return $"{role}: {content}";
     }));

     // Create a termination decision agent using the TerminationStrategy.prompty
     ChatClientAgentOptions agentOptions = new(
         name: "TerminationDecider",
         instructions: PromptFactory.Termination(historyText))
     {
         ChatOptions = new()
         {
             ResponseFormat = Microsoft.Extensions.AI.ChatResponseFormat.ForJsonSchema(
                 schema: AIJsonUtilities.CreateJsonSchema(typeof(TerminationInfo)),
                 schemaName: "TerminationInfo",
                 schemaDescription: "Information about whether the conversation should continue or terminate.")
         }
     };

     var terminationAgent = _chatClient.CreateAIAgent(agentOptions);

     try
     {
         // Get the termination decision from the AI agent
         var response = await terminationAgent.RunAsync(history);
         var terminationInfo = response.Deserialize<TerminationInfo>(JsonSerializerOptions.Web);

         var shouldContinue = terminationInfo?.ShouldContinue ?? true;
         var reason = terminationInfo?.Reason ?? "No reason provided";

         // Log the termination decision
         _logCallback?.Invoke("ShouldTerminateAsync", $"{{Continue: {shouldContinue.ToString()}, Reason: {reason}}}");

         // Return true if we should terminate (i.e., should NOT continue)
         return !shouldContinue;
     }
     catch (Exception ex)
     {
         _logCallback?.Invoke("ShouldTerminateWithAI Error", ex.Message);
         // Default to continue if there's an error
         return false;
     }

 }
```

### Replace Agent with a GroupChat Workflow

Until now the responses we received were from a single agent, lets use AgentGroupChat to orchestrate a chat where multiple agents participate.

1. In VS Code, navigate to **Services/AgentFrameworkService.cs**
1. Search for **//TO DO: Add RunGroupChatOrchestration** and paste the code below
```csharp
    /// <summary>
    /// Orchestrates the group chat with AI agents.
    /// </summary>
    private async Task<(string responseText, string selectedAgentName)> RunGroupChatOrchestration(
        List<ChatMessage> chatHistory,
        string tenantId,
        string userId)
    {
        try
        {
            _logger.LogInformation("Starting Agent Framework Group Chat");
                       
            // Add system context
            chatHistory.Add(new ChatMessage(ChatRole.System, $"User Id: {userId}, Tenant Id: {tenantId}"));

            // Create custom termination function
            var customTerminationFunc = CreateCustomTerminationFunction();

            // Create the workflow
            var workflow = AgentWorkflowBuilder.CreateGroupChatBuilderWith(agents =>
                    new GroupChatWorkflowHelper(_agents!, _chatClient, LogMessage, customTerminationFunc)
                    {
                        MaximumIterationCount = 5
                    })
                    .AddParticipants(_agents!)
                    .Build();

            //run the workflow
            var (responseMessages, selectedAgentName) = await RunWorkflowAsync(workflow,chatHistory);

            //log the function calls from the response messages
            for (int i = chatHistory.Count; i < responseMessages.Count; i++)
            {
                if (responseMessages[i].Role.Value == "assistant")
                {
                    foreach (var content in responseMessages[i].Contents)
                    {
                        // Enhanced logging based on content type
                        switch (content)
                        {
                            case FunctionCallContent functionCall:
                                LogMessage("Function Call", $"Name: {functionCall.Name}, CallId: {functionCall.CallId}");
                                LogMessage("Function Arguments", JsonSerializer.Serialize(functionCall.Arguments, new JsonSerializerOptions { WriteIndented = true }));
                                break;
                        }
                    }
                }
            }

            if (selectedAgentName == "__")
            {
                _logger.LogError("Error in getting response");
                return ("Sorry, I encountered an error while processing your request. Please try again.", "Error");
            }
            // Extract response text
            string responseText = ExtractResponseText(responseMessages);

            _logger.LogInformation("Agent Framework orchestration completed with agent: {AgentName}", selectedAgentName);

            return (responseText, selectedAgentName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in Agent Framework orchestration");
            return ("Sorry, I encountered an error while processing your request. Please try again.", "Error");
        }
    }

```

1. Next replace the **GetResponse()** method with the code below.


```csharp
/// <summary>
/// Processes a user message and returns the agent's response.
/// </summary>
/// <param name="userMessage">The user's message.</param>
/// <param name="messageHistory">The conversation history.</param>
/// <param name="bankService">The banking data service.</param>
/// <param name="tenantId">The tenant identifier.</param>
/// <param name="userId">The user identifier.</param>
/// <returns>A tuple containing the response messages and debug logs.</returns>
public async Task<Tuple<List<Message>, List<DebugLog>>> GetResponse(
    Message userMessage,
    List<Message> messageHistory,
    BankingDataService bankService,
    string tenantId,
    string userId)
{
    try
    {
        var (responseText, selectedAgentName) = await RunGroupChatOrchestration(chatHistory, tenantId, userId);

        return CreateResponseTuple(userMessage, responseText, selectedAgentName);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error when getting response: {ErrorMessage}", ex.Message);
        return new Tuple<List<Message>, List<DebugLog>>(new List<Message>(), new List<DebugLog>());
    }
}
```

## Activity 3: Test your Work

In the previous module we tested each agent independently. With the code changes in this module we should now be able to orchestrate a multi-agent chat where agent selection is automated based on the SelectionStrategy and agent prompts. Lets go ahead and test if the code works as expected.

### Start the Backend

- Return to the open terminal for the backend app in VS Code and type `dotnet run`

### Start a Chat Session

For each response in our testing below, click on the *Bug* icon to see the Debug log to understand the agent selection and termination strategy.
![Debug Log](./media/module-04/view-debuglog.png)

1. Return to the frontend application in your browser.
1. Start a new conversation.
1. Try the below prompts. Provide more information if prompted.
    1. Who can help me here?
    1. Transfer $50 to my friend.
    1. When prompted for account and email, enter, "Account is Acc001 and Email is sandeep@contoso.com"
    1. Looking for a Savings account with high interest rate.
    1. File a complaint about theft from my account.
    1. When prompted confirm its the same account or enter a new account (Acc001 to Acc009) and provide any details it asks for.
    1. How much did I spend on groceries? (If prompted, say over the last 6 months)
    1. Provide me a statement of my account. (If prompted, give it an account number ranging from *Acc001* to *Acc009*)

### Stop the Application

1. In the backend terminal, press **Ctrl + C** to stop the application.

## Validation Checklist

- [ ] Depending on the user prompt the agent selection is dynamic.
- [ ] All the agents  context of the previous messages in teh conversation.
- [ ] The agents are able to invoke the right plugin function to interact with **BankingService**.
- [ ] Vector search  works as expected.

## Common Issues and Solutions

1. Multiple agents respond together or Wrong agent responding:

   - View the 'DebugLog' by using the **Bug** icon in each impacted AI response.
   - Study the Termination Reason
   - Edit the appropriate Prompty files to resolve the conflict.

1. Agent response are invalid:

   - Change in model and/or its version can cause invalid/irrelevant agent behavior.
   - Thorough testing with updated prompts will be required.

## Module Solution

The following sections include the completed code for this Module. Copy and paste these into your project if you run into issues and cannot resolve.

<details>
  <summary>Completed code for <strong>\Services\AgentFrameworkService.cs</strong></summary>
<br>

```csharp
using Microsoft.Extensions.Options;
using Microsoft.Agent Framework;
using Microsoft.Agent Framework.Connectors.OpenAI;
using MultiAgentCopilot.Helper;
using Microsoft.Agent Framework.ChatCompletion;
using Microsoft.Agent Framework.Embeddings;
using Microsoft.Extensions.AI;

using Azure.Identity;
using MultiAgentCopilot.Factories;
using MultiAgentCopilot.Models.Debug;
using MultiAgentCopilot.Models.Chat;
using MultiAgentCopilot.Models.Configuration;

using System.Text;
using MultiAgentCopilot.Models;
using Microsoft.Agent Framework.Agents;
using AgentFactory = MultiAgentCopilot.Factories.AgentFactory;
namespace MultiAgentCopilot.Services;

public class Agent FrameworkService :  IDisposable
{
    readonly Agent FrameworkServiceSettings _skSettings;
    readonly ILoggerFactory _loggerFactory;
    readonly ILogger<Agent FrameworkService> _logger;
    readonly Kernel _Agent Framework;


    bool _serviceInitialized = false;
    string _prompt = string.Empty;
    string _contextSelectorPrompt = string.Empty;

    List<LogProperty> _promptDebugProperties;

    public bool IsInitialized => _serviceInitialized;

    public Agent FrameworkService(
        IOptions<Agent FrameworkServiceSettings> skOptions,
        ILoggerFactory loggerFactory)
    {
        _skSettings = skOptions.Value;
        _loggerFactory = loggerFactory;
        _logger = _loggerFactory.CreateLogger<Agent FrameworkService>();
        _promptDebugProperties = new List<LogProperty>();

        _logger.LogInformation("Initializing the Semantic Kernel service...");

        var builder = Kernel.CreateBuilder();

        //TO DO: Update Agent FrameworkService constructor
        builder.Services.AddSingleton<ILoggerFactory>(loggerFactory);

        DefaultAzureCredential credential;
        if (string.IsNullOrEmpty(_skSettings.AzureOpenAISettings.UserAssignedIdentityClientID))
        {
            credential = new DefaultAzureCredential();
        }
        else
        {
            credential = new DefaultAzureCredential(new DefaultAzureCredentialOptions
            {
                ManagedIdentityClientId = _skSettings.AzureOpenAISettings.UserAssignedIdentityClientID
            });
        }

        builder.AddAzureOpenAIChatCompletion(
           _skSettings.AzureOpenAISettings.CompletionsDeployment,
           _skSettings.AzureOpenAISettings.Endpoint,
           credential);

        _Agent Framework = builder.Build();


        Task.Run(Initialize).ConfigureAwait(false);
    }


    private void LogMessage(string key, string value)
    {
        _promptDebugProperties.Add(new LogProperty(key, value));
    }

    //TO DO: Add GetResponse function

    public async Task<Tuple<List<Message>, List<DebugLog>>> GetResponse(Message userMessage, List<Message> messageHistory, BankingDataService bankService, string tenantId, string userId)
    {
        try
        {
            AgentFactory agentFactory = new AgentFactory();

            var agentGroupChat = agentFactory.BuildAgentGroupChat(_Agent Framework, _loggerFactory, LogMessage, bankService, tenantId, userId);

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

                    // TO DO : Add DebugLog code here


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

    //TO DO: Add Summarize function
    public async Task<string> Summarize(string sessionId, string userPrompt)
    {
        try
        {
            // Use an AI function to summarize the text in 2 words
            var summarizeFunction = _Agent Framework.CreateFunctionFromPrompt(
                "Summarize the following text into exactly two words:\n\n{{$input}}",
                executionSettings: new OpenAIPromptExecutionSettings { MaxTokens = 10 }
            );

            // Invoke the function
            var summary = await _Agent Framework.InvokeAsync(summarizeFunction, new() { ["input"] = userPrompt });

            return summary.GetValue<string>() ?? "No summary generated";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error when getting response: {ErrorMessage}", ex.ToString());
            return string.Empty;
        }
    }



    private Task Initialize()
    {
        try
        {
            _serviceInitialized = true;
            _logger.LogInformation("Semantic Kernel service initialized.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Semantic Kernel service was not initialized. The following error occurred: {ErrorMessage}.", ex.ToString());
        }
        return Task.CompletedTask;
    }       

    public void Dispose()
    {
        // Dispose resources if any
    }
} 
```

</details>

<details>
  <summary>Completed code for <strong>\Factories\AgentFactory.cs</strong></summary>
<br>

```csharp
using Microsoft.Agent Framework;
using Microsoft.Agent Framework.Agents;
using Microsoft.Agent Framework.Agents.Chat;
using Microsoft.Agent Framework.ChatCompletion;
using Microsoft.Agent Framework.Connectors.OpenAI;
using Microsoft.Agent Framework.Connectors.AzureOpenAI;

using OpenAI.Chat;
using System.Text.Json;
using MultiAgentCopilot.StructuredFormats;
using MultiAgentCopilot.Models.ChatInfoFormats;
using MultiAgentCopilot.Logs;
using MultiAgentCopilot.Models;
using MultiAgentCopilot.Services;
using static MultiAgentCopilot.StructuredFormats.ChatResponseFormatBuilder;
using MultiAgentCopilot.Plugins;
using Microsoft.Identity.Client;


namespace MultiAgentCopilot.Factories
{
    internal class AgentFactory
    {
        public delegate void LogCallback(string key, string value);

        private string GetAgentName(AgentType agentType)
        {

            string name = string.Empty;
            switch (agentType)
            {
                case AgentType.Sales:
                    name = "Sales";
                    break;
                case AgentType.Transactions:
                    name = "Transactions";
                    break;
                case AgentType.CustomerSupport:
                    name = "CustomerSupport";
                    break;
                case AgentType.Coordinator:
                    name = "Coordinator";
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(agentType), agentType, null);
            }

            return name;
        }

        private string GetAgentPrompts(AgentType agentType)
        {

            string promptFile = string.Empty;
            switch (agentType)
            {
                case AgentType.Sales:
                    promptFile = "Sales.prompty";
                    break;
                case AgentType.Transactions:
                    promptFile = "Transactions.prompty";
                    break;
                case AgentType.CustomerSupport:
                    promptFile = "CustomerSupport.prompty";
                    break;
                case AgentType.Coordinator:
                    promptFile = "Coordinator.prompty";
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(agentType), agentType, null);
            }

            string prompt = $"{File.ReadAllText("Prompts/" + promptFile)}{File.ReadAllText("Prompts/CommonAgentRules.prompty")}";

            return prompt;
        }



        public ChatCompletionAgent BuildAgent(Kernel kernel, AgentType agentType, ILoggerFactory loggerFactory, BankingDataService bankService, string tenantId, string userId)
        {
            ChatCompletionAgent agent = new ChatCompletionAgent
            {
                Name = GetAgentName(agentType),
                Instructions = $"""{GetAgentPrompts(agentType)}""",
                Kernel = GetAgentKernel(kernel, agentType, loggerFactory, bankService, tenantId, userId),
                Arguments = new KernelArguments(new AzureOpenAIPromptExecutionSettings() { FunctionChoiceBehavior = FunctionChoiceBehavior.Auto() })
            };

            return agent;
        }

        private Kernel GetAgentKernel(Kernel kernel, AgentType agentType, ILoggerFactory loggerFactory, BankingDataService bankService, string tenantId, string userId)
        {
            Kernel agentKernel = kernel.Clone();
            switch (agentType)
            {
                case AgentType.Sales:
                    var salesPlugin = new SalesPlugin(loggerFactory.CreateLogger<SalesPlugin>(), bankService, tenantId, userId);
                    agentKernel.Plugins.AddFromObject(salesPlugin);
                    break;
                case AgentType.Transactions:
                    var transactionsPlugin = new TransactionPlugin(loggerFactory.CreateLogger<TransactionPlugin>(), bankService, tenantId, userId);
                    agentKernel.Plugins.AddFromObject(transactionsPlugin);
                    break;
                case AgentType.CustomerSupport:
                    var customerSupportPlugin = new CustomerSupportPlugin(loggerFactory.CreateLogger<CustomerSupportPlugin>(), bankService, tenantId, userId);
                    agentKernel.Plugins.AddFromObject(customerSupportPlugin);
                    break;
                case AgentType.Coordinator:
                    var CoordinatorPlugin = new CoordinatorPlugin(loggerFactory.CreateLogger<CoordinatorPlugin>(), bankService, tenantId, userId);
                    agentKernel.Plugins.AddFromObject(CoordinatorPlugin);
                    break;
                default:
                    throw new ArgumentException("Invalid plugin name");
            }

            return agentKernel;
        }



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
                    {{{GetStrategyPrompts(strategyType)}}}
                    
                    RESPONSE:
                    {{$lastmessage}}
                    """,
                    safeParameterNames: "lastmessage");

            return function;
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
                                logCallback("SELECTION - Agent", ContinuationInfo!.AgentName); 
                                logCallback("SELECTION - Reason", ContinuationInfo!.Reason);                       
                                return ContinuationInfo!.AgentName;
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
                                logCallback("TERMINATION - Continue", terminationInfo!.ShouldContinue.ToString()); 
                                logCallback("TERMINATION - Reason", terminationInfo!.Reason); 
                                return !terminationInfo!.ShouldContinue;
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
    
    
```

</details>

## Next Steps

Congratulations!!! You have completed this hands-on-lab!!!

You can see the full source code for this lab for both Semantic Kernel and LangGraph at <https://github.com/AzureCosmosDB/banking-multi-agent-workshop/>. You can also take this lab again or the LangGraph one at <https://github.com/AzureCosmosDB/banking-multi-agent-workshop/tree/hol>. We hope you enjoyed this lab.
