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

Lets add some banking domain functions that wil be used by the various agents.

Update IBankDataService at BankingAPI\Interface to add more function definitions

```csharp

        Task<List<BankTransaction>> GetTransactionsAsync(string tenantId, string accountId, DateTime startDate, DateTime endDate);

        Task<ServiceRequest> CreateFundTransferRequestAsync(string tenantId, string accountId, string userId, string requestAnnotation, string recipientEmail, string recipientPhone, decimal debitAmount);

        Task<ServiceRequest> CreateTeleBankerRequestAsync(string tenantId, string accountId, string userId, string requestAnnotation, DateTime scheduledDateTime);

        Task<ServiceRequest> CreateFulfilmentRequestAsync(string tenantId, string accountId, string userId, string requestAnnotation, Dictionary<string, string> fulfilmentDetails);

        Task<List<ServiceRequest>> GetServiceRequestsAsync(string tenantId, string accountId, string? userId = null, ServiceRequestType? SRType = null);

        Task<bool> AddServiceRequestDescriptionAsync(string tenantId, string accountId, string requestId, string annotationToAdd);

        Task<String> GetTeleBankerAvailabilityAsync();
```

Update BankingDataService at Services\BankingDataService.cs to add function implementations

```csharp


public async Task<List<BankTransaction>> GetTransactionsAsync(string tenantId, string accountId, DateTime startDate, DateTime endDate)
        {
            try
            {
                var partitionKey = PartitionManager.GetAccountsDataFullPK(tenantId, accountId);

                QueryDefinition queryDefinition = new QueryDefinition(
                       "SELECT * FROM c WHERE c.accountId = @accountId AND c.transactionDateTime >= @startDate AND c.transactionDateTime <= @endDate AND c.type = @type")
                       .WithParameter("@accountId", accountId)
                       .WithParameter("@type", nameof(BankTransaction))
                       .WithParameter("@startDate", startDate)
                       .WithParameter("@endDate", endDate);

                List<BankTransaction> transactions = new List<BankTransaction>();
                using (FeedIterator<BankTransaction> feedIterator = _accountData.GetItemQueryIterator<BankTransaction>(queryDefinition, requestOptions: new QueryRequestOptions { PartitionKey = partitionKey }))
                {
                    while (feedIterator.HasMoreResults)
                    {
                        //var abc= await feedIterator.ReadNextAsync();
                        FeedResponse<BankTransaction> response = await feedIterator.ReadNextAsync();
                        transactions.AddRange(response);
                    }
                }

                return transactions;
            }
            catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                _logger.LogError(ex.ToString());
                return new List<BankTransaction>();
            }
        }

        public async Task<ServiceRequest> CreateFundTransferRequestAsync(string tenantId, string accountId, string userId, string requestAnnotation, string recipientEmail, string recipientPhone, decimal debitAmount)
        {
            var req= new ServiceRequest(ServiceRequestType.FundTransfer, tenantId, accountId, userId, requestAnnotation, recipientEmail, recipientPhone, debitAmount,  DateTime.MinValue,null);
            return await AddServiceRequestAsync(req);
        }

        public async Task<ServiceRequest> CreateTeleBankerRequestAsync(string tenantId, string accountId, string userId, string requestAnnotation, DateTime scheduledDateTime)
        {
            var req = new ServiceRequest(ServiceRequestType.TeleBankerCallBack, tenantId, accountId, userId, requestAnnotation, string.Empty, string.Empty, 0,  scheduledDateTime,null);
            return await AddServiceRequestAsync(req);
        }

        public Task<string> GetTeleBankerAvailabilityAsync()
        {
            return Task.FromResult("Monday to Friday, 8 AM to 8 PM Pacific Time");
        }
        public async Task<ServiceRequest> CreateFulfilmentRequestAsync(string tenantId, string accountId, string userId, string requestAnnotation, Dictionary<string,string> fulfilmentDetails)
        {
            var req = new ServiceRequest(ServiceRequestType.Fulfilment, tenantId, accountId, userId, requestAnnotation, string.Empty, string.Empty, 0,  DateTime.MinValue, fulfilmentDetails);
            return await AddServiceRequestAsync(req);
        }
        public async Task<List<ServiceRequest>> GetServiceRequestsAsync(string tenantId, string accountId, string? userId = null, ServiceRequestType? SRType = null)
        {
            try
            {
                var partitionKey = PartitionManager.GetAccountsDataFullPK(tenantId, accountId);

                var queryBuilder = new StringBuilder("SELECT * FROM c WHERE c.type = @type");
                var queryDefinition = new QueryDefinition(queryBuilder.ToString())
                      .WithParameter("@type", nameof(ServiceRequest));

                if (!string.IsNullOrEmpty(userId))
                {
                    queryBuilder.Append(" AND c.userId = @userId");
                    queryDefinition = queryDefinition.WithParameter("@userId", userId);
                }

                if (SRType.HasValue)
                {
                    queryBuilder.Append(" AND c.SRType = @SRType");
                    queryDefinition = queryDefinition.WithParameter("@SRType", SRType);
                }


                List<ServiceRequest> reqs = new List<ServiceRequest>();
                using (FeedIterator<ServiceRequest> feedIterator = _requestData.GetItemQueryIterator<ServiceRequest>(queryDefinition, requestOptions: new QueryRequestOptions { PartitionKey = partitionKey }))
                {
                    while (feedIterator.HasMoreResults)
                    {
                        FeedResponse<ServiceRequest> response = await feedIterator.ReadNextAsync();
                        reqs.AddRange(response);
                    }
                }

                return reqs;
            }
            catch (CosmosException ex)
            {
                _logger.LogError(ex.ToString());
                return new List<ServiceRequest>();
            }
        }



        public async Task<bool> AddServiceRequestDescriptionAsync(string tenantId, string accountId, string requestId, string annotationToAdd)
        {
            try
            {
                var partitionKey = PartitionManager.GetAccountsDataFullPK(tenantId, accountId);

                var patchOperations = new List<PatchOperation>
                {
                    PatchOperation.Add("/requestAnnotations/-", $"[{DateTime.Now.ToUniversalTime().ToString()}] : {annotationToAdd}")
                };

                ItemResponse<ServiceRequest> response = await _requestData.PatchItemAsync<ServiceRequest>(
                    id: requestId,
                    partitionKey: partitionKey,
                    patchOperations: patchOperations
                );

                return true;
            }
            catch (CosmosException ex)
            {
                _logger.LogError(ex.ToString());
                return false;
            }
        }
       

```

Update \ChatInfrastructure\AgentPlugins\CustomerSupportPlugin.cs with additional functions  

```csharp

   [KernelFunction("CheckPendingServiceRequests")]
   [Description("Search the database for pending requests")]
   public async Task<List<ServiceRequest>> CheckPendingServiceRequests(string? accountId = null, ServiceRequestType? srType = null)
   {
      _logger.LogTrace($"Searching database for matching requests for Tenant: {_tenantId} User: {_userId}");

      return await _bankService.GetServiceRequestsAsync(_tenantId, accountId ?? string.Empty, null, srType);
   }

   [KernelFunction]
   [Description("Adds a telebanker callback request for the specified account.")]
   public async Task<ServiceRequest> AddTeleBankerRequest(string accountId,string requestAnnotation ,DateTime callbackTime)
   {
      _logger.LogTrace($"Adding Tele Banker request for Tenant: {_tenantId} User: {_userId}, account: {accountId}");

      return await _bankService.CreateTeleBankerRequestAsync(_tenantId, accountId,_userId, requestAnnotation, callbackTime);
   }

   [KernelFunction]
   [Description("Get list of availble slots for telebankers specializng in an account type")]
   public async Task<string> GetTeleBankerSlots(AccountType accountType)
   {
      _logger.LogTrace($"Checking availability for Tele Banker for Tenant: {_tenantId} AccountType: {accountType.ToString()}");

      return await _bankService.GetTeleBankerAvailabilityAsync();
   }



   [KernelFunction]
   [Description("Updates an existing service request with additional details")]
   public async Task<bool> UpdateExistingServiceRequest(string requestId, string accountId, string requestAnnotation)
   {
      _logger.LogTrace($"Updating service request for Request: {requestId}");

      return await  _bankService.AddServiceRequestDescriptionAsync(_tenantId, accountId, requestId, requestAnnotation);
   }

```


Update ChatInfrastructure\AgentPlugins\SalesPlugin.cs with additional functions  

```csharp

        [KernelFunction]
        [Description("Register a new account.")]
        public async Task<ServiceRequest> RegisterAccount(string userId, AccountType accType, Dictionary<string,string> fulfilmentDetails)
        {
            _logger.LogTrace($"Registering Account. User ID: {userId}, Account Type: {accType}");
            return await _bankService.CreateFulfilmentRequestAsync(_tenantId, string.Empty,_userId,string.Empty,fulfilmentDetails);
        }
```

Update ChatInfrastructure\AgentPlugins\TransactionPlugin.cs with additional functions  

```csharp
    [KernelFunction]
    [Description("Adds a new Account Transaction request")]
    public async Task<ServiceRequest> AddFunTransferRequest(
       string debitAccountId,
       decimal amount,
       string requestAnnotation,
       string? recipientPhoneNumber = null,
       string? recipientEmailId = null)
    {
       _logger.LogTrace("Adding AccountTransaction request for User ID: {UserId}, Debit Account: {DebitAccountId}", _userId, debitAccountId);
    
       // Ensure non-null values for recipientEmailId and recipientPhoneNumber
       string emailId = recipientEmailId ?? string.Empty;
       string phoneNumber = recipientPhoneNumber ?? string.Empty;
    
       return await _bankService.CreateFundTransferRequestAsync(_tenantId, debitAccountId, _userId, requestAnnotation, emailId, phoneNumber, amount);
    }
    
    
    [KernelFunction]
    [Description("Get the transactions history between 2 dates")]
    public async Task<List<BankTransaction>> GetTransactionHistory(string accountId, DateTime startDate, DateTime endDate)
    {
       _logger.LogTrace("Fetching AccountTransaction history for Account: {AccountId}, From: {StartDate} To: {EndDate}", accountId, startDate, endDate);
       return await _bankService.GetTransactionsAsync(_tenantId, accountId, startDate, endDate);
    
    }

```


### Add Selection Strategy for Agent Selection

When dealing with multiple agents, deciding which agent should respond is critical. We don't all agents to respond to all user prompts. SelectionStrategy is  the mechanism in SemanticKernel to decide the next participant. The SelectionStrategy is defined in natural language.

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

### Add Termination Strategy for Agent reponse

Similar to SelectionStrategy deciding when the agents should stop responding to a user prompt is important, else you may see multiple unwanted agent responses to a user prompt. TerminationStrategy is  the mechanism in SemanticKernel to decide when to stop. The TerminationStrategy is defined in natural language. We want the LLM to return YES if more agent participation is required, else it should be NO.

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

By default  the LLM responds to a user response in natural language. However, we can force the LLM to  use a structure format in its response. A structure format helps us parse the response and use it our code for decision making.

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

Prompts to make the LLM output the Continuation and Termination models as responses

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

### AutoFunctionInvocationLoggingFilter.cs

Helper function to log the kernel function selection in the plugin. This is only required to log and debug.

Add AutoFunctionInvocationLoggingFilter.cs in ChatInfrastructure\Logs

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


````

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
#pragma warning disable CS8604 // Possible null reference argument.
                            var ContinuationInfo = JsonSerializer.Deserialize<ContinuationInfo>(result.GetValue<string>());
#pragma warning restore CS8604 // Possible null reference argument.
                         
                            return ContinuationInfo.AgentName;
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
#pragma warning disable CS8604 // Possible null reference argument.
                            var terminationInfo = JsonSerializer.Deserialize<TerminationInfo>(result.GetValue<string>());
#pragma warning restore CS8604 // Possible null reference argument.

                            return !terminationInfo.ShouldContinue;
                        }
                    },
            };

            return ExecutionSettings;
        }

```

### Add ChatInfrastructure/Helper/AuthorRoleHelper.cs

 This code helps reverse look up the Agent's role when building the agent chat from history stored in Cosmos DB.

```csharp
﻿using Microsoft.SemanticKernel.ChatCompletion;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MultiAgentCopilot.ChatInfrastructure.Helper
{
    internal static class AuthorRoleHelper
    {
        private static readonly Dictionary<string, AuthorRole> RoleMap =
        new(StringComparer.OrdinalIgnoreCase)
        {
            { "system", AuthorRole.System },
            { "assistant", AuthorRole.Assistant },
            { "user", AuthorRole.User },
            { "tool", AuthorRole.Tool }
        };

        public static AuthorRole? FromString(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return null;
            }

            return RoleMap.TryGetValue(name, out var role) ? role : null;
        }
    }
}

```


### Replace Agent with AgentGroupChat in SemanticKernel

Till now the responses we received were from a single agent, lets us use AgentGroupChat to orchestrate a chat were multiple agents participate.

Add the below references in ChatInfrastructure/Factories/ChatFactory.cs

```csharp
using MultiAgentCopilot.ChatInfrastructure.Helper;

```

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
#pragma warning disable CS8604 // Possible null reference argument.
                            var ContinuationInfo = JsonSerializer.Deserialize<ContinuationInfo>(result.GetValue<string>());
#pragma warning restore CS8604 // Possible null reference argument.
                            logCallback("SELECTION - Agent",ContinuationInfo.AgentName); // provides visibility (can use logger)
                            logCallback("SELECTION - Reason",ContinuationInfo.Reason); // provides visibility (can use logger)                            
                            return ContinuationInfo.AgentName;
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
#pragma warning disable CS8604 // Possible null reference argument.
                            var terminationInfo = JsonSerializer.Deserialize<TerminationInfo>(result.GetValue<string>());
#pragma warning restore CS8604 // Possible null reference argument.
                            logCallback("TERMINATION - Continue",terminationInfo.ShouldContinue.ToString()); // provides visibility (can use logger)
                            logCallback("TERMINATION - Reason",terminationInfo.Reason); // provides visibility (can use logger)
                            return !terminationInfo.ShouldContinue;
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
            //_promptDebugProperties.Clear();

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
4. Start a Chat Session in the UI
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

