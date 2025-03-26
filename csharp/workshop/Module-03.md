# Module 03 - Agent Specialization

[< Connecting Agents to Memory](./Module-02.md) - **[Home](Home.md)** - [Multi-Agent Orchestration >](./Module-04.md)

## Introduction

In this Module you'll learn how to implement agent specialization by creating Semantic Kernel Functions or LangGraph Tools that provide the functionality necessary to power individual agents that comprise a multi-agent system.

## Learning Objectives and Activities

- Learn the basics for Semantic Kernel Agent Framework Functions and LangGraph Tools
- Learn how to implement semantic and natural language features using Vector indexing and search integration from Azure Cosmos DB.
- Learn how to define tasks and communication protocols for seamless collaboration.

## Module Exercises

1. [Activity 1: Understanding Agent Specialization and Integration](#activity-1-understanding-agent-specialization-and-integration)  
1. [Activity 2: Defining Bank Domain Data Models](#activity-2-defining-bank-domain-data-models)  
1. [Activity 3: Defining Agent Behavior](#activity-3-defining-agent-behavior)  
1. [Activity 4: Integrating Bank Domain Functions as Plugins](#activity-4-integrating-bank-domain-functions-as-plugins)  
1. [Activity 5: Developing a Plugin Factory](#activity-5-developing-a-plugin-factory)  
1. [Activity 6: Building an Agent Factory](#activity-6-building-an-agent-factory)
1. [Activity 7: Semantic Search](#activity-7-semantic-search)
1. [Activity 8: Bringing It All Together – Bank Domain Models, Plugins, and Agents](#activity-8-bringing-it-all-together--bank-domain-models-plugins-and-agents)
1. [Activity 9: Test your Work](#activity-9-test-your-work)

## Activity 1: Understanding Agent Specialization and Integration

In this session we will dive into how to create Semantic Kernel Agent Framework Functions or LangGraph Tools to connect agents to external APIs, databases and third-party tools to provide special functionality. Learn the basics for vector indexing and search in Azure Cosmos DB to provide semantic search functionality to your agents. Learn how to define tasks and communication protocols for seamless collaboration between agents.

## Activity 2: Defining Bank Domain Data Models

After the session in Activity 1, you should understand the need and importance for agent specialization and have a basic grasp of how to build and integrate them. For the remainder of this module we will do just that for our banking scenario.

When working with any kind of data we need to review our data models.

To begin, navigate to the `Common` project and navigate to the `/Models/Banking' folder. Familiarize yourself with the models used here.

The project and folder structure should look like the following:

![Models folder](./media/module-03/solution-models-banking-folder-start.png)

## Activity 3: Defining Agent Behavior

Agent behavior is defined using prompts. These can be as simple as text in a string variable. However, it is often better to store these as external text files. In this solution we will use a format called, Prompty to manage our prompts.

Prompty is an asset class and file format designed to streamline the development and management of prompts for Large Language Models (LLMs). By combining configuration settings, sample data, and prompt templates into a single .prompty file, Prompty enhances observability, understandability, and portability for developers, thereby accelerating the prompt engineering process.

### Understand Agent behavior using Prompty

In this activity we will review the existing Prompty files.

Look in your IDE, within the `ChatAPI` project, the `/Prompts` folder with the prompty files  should like seen below.

![Prompty files](./media/module-03/solution-prompt-folder.png)

#### Common Agent Rules

This isn't agent but provides a baseline for how agents are supposed to behave. Think of it like a set of global rules for agents. All agents import the text from this prompt to govern their responses. Review the contents of `CommonAgentRules.prompty`.

#### Coordinator Agent

This agent is the coordinator for the entire multi-agent system we are building. Its purpose is own the entire experience for users with the banking agent system. It starts by greeting new users when they initiate a new session, then routes user requests to the correct agent(s) to handle on their behalf. Finally it asks for feedback on how it did its job. Review the contents of `Coordinator.prompty`.

#### Customer Support Agent

This agent handles anything that appears to be a customer support request by a user. It can create, find and update services requests for users. It can also take certain action on behalf of users too. Review the contents of `CustomerSupport.prompty`.

#### Sales Agent

This agent is used when customers ask questions about what kinds of services a bank offers. The data on the products the bank has are stored in Cosmos DB. This agent performs a vector search in Cosmos DB to find the most suitable products for a customer's request. Review the contents of `Sales.prompty`.

#### Transaction Agent

This agent handles any account-based transactions on behalf of the user including getting account balances, generating statements and doing fund transfers between accounts. Review the contents of `Transactions.prompty`.

### Retrieving the prompty text for Agents

In our banking solution we have four agents: transactions agent, sales agent, customer support agent, and a coordinator agent to manage all of them. With the behavior of the agents defined in Prompty, we now need to implement the code that will allow the application to load the agent behavior for each of the agents.

In your IDE, navigate to the `Models` folder within the `ChatInfrastructure` project. Review the contents of `AgentTypes.cs`.

### Implementing the System Prompt Factory

We are now ready to complete the implementation for the **System Prompt Factory** we created in the previous Module. We will do that by first adding a reference to `MultiAgentCopilot.ChatInfrastructure.Models` in `ChatInfrastructure\Factories\SystemPromptFactory.cs`. This will allow it to consume the `AgentType` enum we created in the previous step.

In the same `ChatInfrastructure` project, navigate to the `/Factories` folder.

Open the `SystemPromptFactory.cs`

Add this with the other using statements at the top of the file:

```csharp
using MultiAgentCopilot.ChatInfrastructure.Models;
```

Remain in this same class.

Next we need to replace our original hard-coded implementation from Module 3 to use the AgentType enum for our newly defined banking agents. It is also worth noting that it is here where the contents of the `CommonAgentsRules.prompty` are included as part of the system prompts that define our agents.

Within the `SystemPromptFactory.cs`

Replace the code for both `GetAgentName()` and `GetAgentPrompts()` with the code below:

```csharp
    internal static class SystemPromptFactory
    {
        //Replace from here
        public static string GetAgentName(AgentType agentType)
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

            return name;//.ToUpper();
        }

        public static string GetAgentPrompts(AgentType agentType)
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
        //end replace
    }
```

## Activity 4: Integrating Bank Domain Functions as Plugins

All banking domain code is encapsulated in a separate `BankingServices` project. Let's update it make banking domain functions available to the agent plugins. For simplicity in this workshop, all functions reference BankingServices. However, kernel functions can be any managed code that enables the LLM to interact with the outside world. The Base plugin, inherited by all plugins, contains common code for all plugins.

In your IDE, navigate to the **ChatInfrastructure** project in the solution.

Then navigate to the `ChatInfrastructure\AgentPlugins\` folder.

Open the `BasePlugin.cs` file

Add the three functions below into this class.

```csharp
    [KernelFunction("GetLoggedInUser")]
    [Description("Get the current logged-in BankUser")]
    public async Task<BankUser> GetLoggedInUser()
    {
        _logger.LogTrace($"Get Logged In User for Tenant:{_tenantId}  User:{_userId}");
        return await _bankService.GetUserAsync(_tenantId, _userId);
    }

    [KernelFunction("GetCurrentDateTime")]
    [Description("Get the current date time in UTC")]
    public DateTime GetCurrentDateTime()
    {
        _logger.LogTrace($"Get Datetime: {System.DateTime.Now.ToUniversalTime()}");
        return System.DateTime.Now.ToUniversalTime();
    }

    [KernelFunction("GetUserRegisteredAccounts")]
    [Description("Get user registered accounts")]
    public async Task<List<BankAccount>> GetUserRegisteredAccounts()
    {
        _logger.LogTrace($"Fetching accounts for Tenant: {_tenantId} User ID: {_userId}");
        return await _bankService.GetUserRegisteredAccountsAsync(_tenantId, _userId);
    } 
```

Navigate and open the `SalesPlugin.cs` file.

Copy the following code into the class.

```csharp
    [KernelFunction]
    [Description("Search an offer by name")]
    public async Task<Offer> GetOfferDetailsByName(string offerName)
    {
        _logger.LogTrace($"Fetching Offer by name");
        return await _bankService.GetOfferDetailsByNameAsync(_tenantId, offerName);
    }

    [KernelFunction]
    [Description("Get the transactions history between 2 dates")]
    public async Task<List<BankTransaction>> GetTransactionHistory(string accountId, DateTime startDate, DateTime endDate)
    {
        _logger.LogTrace("Fetching AccountTransaction history for Account: {AccountId}, From: {StartDate} To: {EndDate}", accountId, startDate, endDate);
        return await _bankService.GetTransactionsAsync(_tenantId, accountId, startDate, endDate);
    }

    [KernelFunction]
    [Description("Register a new account.")]
    public async Task<ServiceRequest> RegisterAccount(string userId, AccountType accType, Dictionary<string,string> fulfilmentDetails)
    {
        _logger.LogTrace($"Registering Account. User ID: {userId}, Account Type: {accType}");
        return await _bankService.CreateFulfilmentRequestAsync(_tenantId, string.Empty,_userId,string.Empty,fulfilmentDetails);
    }  
```

Navigate to and open the `CustomerSupportPlugin.cs` file

Copy the following code into the class.

```csharp
    [KernelFunction("IsAccountRegisteredToUser")]
    [Description("Check if account is registered to user")]
    public async Task<bool> IsAccountRegisteredToUser(string accountId)
    {
        _logger.LogTrace($"Validating account for Tenant: {_tenantId} User ID: {_userId}- {accountId}");
        var accountDetails = await _bankService.GetAccountDetailsAsync(_tenantId, _userId, accountId);
        return accountDetails != null;
    }

    [KernelFunction]
    [Description("Create new complaint")]
    public async Task<ServiceRequest> CreateComplaint(string accountId, string requestAnnotation)
    {
        _logger.LogTrace($"Adding new service request for Tenant: {_tenantId} User: {_userId}, Account: {accountId}");
        return await _bankService.CreateComplaintAsync(_tenantId, accountId, _userId, requestAnnotation);
    }

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
   [Description("Get list of available slots for telebankers specializing in an account type")]
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

Navigate to and open the `TransactionPlugin.cs` file

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

## Activity 5: Developing a Plugin Factory

Next we will create a  **PluginFactory** that dynamically generates a plugin based on the agent type.

In the `ChatInfrastructure` project, navigate to the `/Factories` folder

Create a new file, `PluginFactory.cs`

Replace the contents of the file with the code below.

```csharp
using Microsoft.SemanticKernel;
using MultiAgentCopilot.ChatInfrastructure.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MultiAgentCopilot.ChatInfrastructure.Plugins;
using BankingServices.Interfaces;
using MultiAgentCopilot.ChatInfrastructure.Models;

namespace MultiAgentCopilot.ChatInfrastructure.Factories
{
    internal static class PluginFactory
    {
        internal static Kernel GetAgentKernel(Kernel kernel, AgentType agentType, ILoggerFactory loggerFactory, IBankDataService bankService, string tenantId, string userId)
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
    }
}
```

## Activity 6: Building an Agent Factory

Next we will modify the `BuildAgent()` function within the `ChatFactory` class to dynamically add plugins to the agents.

Within the `/Factories` folder, open the `ChatFactory.cs` file.

Add the following lines of code to the `usings` at the top of the file.

```csharp
using BankingServices.Interfaces;
using MultiAgentCopilot.ChatInfrastructure.Models;
```

Next, replace the `BuildAgent()` function with this code below.

```csharp
    public ChatCompletionAgent BuildAgent(Kernel kernel, AgentType agentType, ILoggerFactory loggerFactory, IBankDataService bankService, string tenantId, string userId)
    {
        ChatCompletionAgent agent = new ChatCompletionAgent
        {
            Name = SystemPromptFactory.GetAgentName(agentType),
            Instructions = $"""{SystemPromptFactory.GetAgentPrompts(agentType)}""",
            Kernel = PluginFactory.GetAgentKernel(kernel, agentType,loggerFactory, bankService, tenantId, userId),
            Arguments = new KernelArguments(new AzureOpenAIPromptExecutionSettings() { FunctionChoiceBehavior = FunctionChoiceBehavior.Auto() })
        };
    
        return agent;
    }
```

Within the `ChatInfrastructure` project, navigate to the `/Services` folder

Open the `ChatServices.cs` file

Replace the constructor for the ChatService with the code below to initialize the BankingDataService.

```csharp
public ChatService(
        IOptions<CosmosDBSettings> cosmosOptions,
        IOptions<SemanticKernelServiceSettings> skOptions,
        ICosmosDBService cosmosDBService,
        ISemanticKernelService skService,
        ILoggerFactory loggerFactory)
    {
        _cosmosDBService = cosmosDBService;
        _skService = skService;
        _bankService = new BankingDataService(cosmosOptions.Value, skOptions.Value, loggerFactory);
        _logger = loggerFactory.CreateLogger<ChatService>();
    }
```

## Activity 7: Semantic Search

In this activity, you will learn how to configure vector indexing and search in Azure Cosmos DB and explore the container and vector indexing policies. Then learn how to implement vector search using for Semantic Kernel.

### Create Data Model for Vector Search

Data Models used for Vector Search in Semantic Kernel need to be enhanced with additional attributes. We will use `OfferTerm` as vector search enabled data model.

To add the `OfferTerm` model, navigate to the `Common` project and navigate to the `/Models/Banking' folder

Create a new class, `OfferTerm.cs`

Replace the code with this code below for creating an OfferTerm class

```csharp
using Microsoft.Extensions.VectorData;
namespace MultiAgentCopilot.Common.Models.Banking
{
    public class OfferTerm
    {
        [VectorStoreRecordKey]
        public required string Id { get; set; }

        [VectorStoreRecordData]
        public required string TenantId { get; set; }

        [VectorStoreRecordData]
        public required string OfferId { get; set; }

        [VectorStoreRecordData]
        public required string Name { get; set; }

        [VectorStoreRecordData]
        public required string Text { get; set; }

        [VectorStoreRecordData]
        public required string Type { get; set; }

        [VectorStoreRecordData]
        public required string AccountType { get; set; }

        [VectorStoreRecordVector(Dimensions: 1536, DistanceFunction: DistanceFunction.CosineSimilarity, IndexKind: IndexKind.QuantizedFlat)]
        public ReadOnlyMemory<float>? Vector { get; set; }
    }
}
```

### Update BankingDataService to include vector search

In your IDE, navigate to the `BankingServices` project.

Then navigate to the `/Interface` folder.

Open the `IBankDataService.cs` file.

Paste the two interface definitions in the public interface defintion.

```csharp
    Task<List<OfferTerm>> SearchOfferTermsAsync(string tenantId, AccountType accountType, string requirementDescription);
    Task<Offer> GetOfferDetailsAsync(string tenantId, string offerId);
```

Next, navigate to the `/Services` folder.

Open the `BankingDataService.cs` file.

Add the two class-level variables to the file.

```csharp
        private readonly AzureCosmosDBNoSQLVectorStoreRecordCollection<OfferTerm> _offerDataVectorStore;
        private readonly AzureOpenAITextEmbeddingGenerationService _textEmbeddingGenerationService;
```

Scroll down in the file until you see, `_semanticKernel = builder.Build();`

Paste the following code below it.

```csharp
        _textEmbeddingGenerationService = new(
            deploymentName: skSettings.AzureOpenAISettings.EmbeddingsDeployment,
            endpoint: skSettings.AzureOpenAISettings.Endpoint,
            credential: credential);

        var vectorStoreOptions = new AzureCosmosDBNoSQLVectorStoreRecordCollectionOptions<OfferTerm> { PartitionKeyPropertyName = "TenantId", JsonSerializerOptions = jsonSerializerOptions };
        _offerDataVectorStore = new AzureCosmosDBNoSQLVectorStoreRecordCollection<OfferTerm>(_database, _settings.OfferDataContainer.Trim(), vectorStoreOptions);
```

Next, we will implement the two new functions we added to the `IBankDataService.cs` interface earlier.

Below the function in which you just pasted the code above, paste the following two functions.

```csharp
        public async Task<List<OfferTerm>> SearchOfferTermsAsync(string tenantId, AccountType accountType, string requirementDescription)
        {           
            try
            {
                // Generate Embedding
                ReadOnlyMemory<float> embedding = (await _textEmbeddingGenerationService.GenerateEmbeddingsAsync(
                       new[] { requirementDescription }
                   )).FirstOrDefault();

                // perform vector search
                var filter = new VectorSearchFilter()
                    .EqualTo("TenantId", tenantId)
                    .EqualTo("Type", "Term")
                    .EqualTo("AccountType", "Savings");
                var options = new VectorSearchOptions { VectorPropertyName = "Vector", Filter = filter, Top = 10, IncludeVectors = false };
                                
                var searchResults = await _offerDataVectorStore.VectorizedSearchAsync(embedding, options);
        
                List<OfferTerm> offerTerms = new();
                await foreach (var result in searchResults.Results)
                {
                    offerTerms.Add(result.Record);
                }
                return offerTerms;
            }
            catch(Exception ex)
            {
                _logger.LogError(ex.ToString());
                return new List<OfferTerm>();
            }           
        }

        public async Task<Offer> GetOfferDetailsAsync(string tenantId, string offerId)
        {
            try
            {
                var partitionKey = new PartitionKey(tenantId);

                return await _offerData.ReadItemAsync<Offer>(
                       id: offerId,
                       partitionKey: new PartitionKey(tenantId));
            }
            catch (CosmosException ex)
            {
                _logger.LogError(ex.ToString());
                return null;
            }
        }
```

In your IDE, navigate to the `ChatInfrastructure` project.

Then navigate to the `/AgentPlugins` folder.

Open the `SalesPlugin.cs` file.

Add these two functions to perform vector searches

```csharp
    [KernelFunction]
    [Description("Search offer terms of all available offers using vector search")]
    public async Task<List<OfferTerm>> SearchOfferTerms(AccountType accountType, string requirementDescription)
    {
        _logger.LogTrace($"Searching terms of all available offers matching '{requirementDescription}'");
        return await _bankService.SearchOfferTermsAsync(_tenantId, accountType, requirementDescription);
    }
    
    [KernelFunction]
    [Description("Get detail for an offer")]
    public async Task<Offer> GetOfferDetails(string offerId)
    {
        _logger.LogTrace($"Fetching Offer");
        return await _bankService.GetOfferDetailsAsync(_tenantId, offerId);
    }
```

## Activity 8: Bringing It All Together – Bank Domain Models, Plugins, and Agents

We are nearly complete with the updates for our multi-agent application.

Within the `ChatInfrastructure` project, navigate to the `/Interfaces` folder.

Open the `ISemanticKernelService.cs` file.

Copy the following code to modify our `GetResponse()` function.

```csharp
    Task<Tuple<List<Message>, List<DebugLog>>> GetResponse(Message userMessage, List<Message> messageHistory, IBankDataService bankService, string tenantId, string userId);
```

Next, navigate to the `/Services` folder.

Open the `SemanticKernelService.cs` file.

Paste the two lines of code below to add a reference for MultiAgentCopilot.ChatInfrastructure.Models and BankingServices.Interfaces

```csharp
using MultiAgentCopilot.ChatInfrastructure.Models;
using BankingServices.Interfaces;
```

Then modify the `GetResponse()` function to implement the updated interface.

Replace the function with the code below.

```csharp
 public async Task<Tuple<List<Message>, List<DebugLog>>> GetResponse(Message userMessage, List<Message> messageHistory, IBankDataService bankService, string tenantId, string userId)
    {
        try
        {
            ChatFactory agentChatGeneratorService = new ChatFactory();

            var agent = agentChatGeneratorService.BuildAgent(_semanticKernel,AgentType.CustomerSupport, _loggerFactory,  bankService, tenantId, userId);

            ChatHistory chatHistory = [];

            // Load history
            foreach (var chatMessage in messageHistory)
            {                
                if(chatMessage.SenderRole == "User")
                {
                    chatHistory.AddUserMessage(chatMessage.Text);
                }
                else
                {
                    chatHistory.AddAssistantMessage(chatMessage.Text);
                }
            }

            chatHistory.AddUserMessage(userMessage.Text);

            _promptDebugProperties = new List<LogProperty>();

            List<Message> completionMessages = new();
            List<DebugLog> completionMessagesLogs = new();

            await foreach (ChatMessageContent response in agent.InvokeAsync(chatHistory))
            {
                string messageId = Guid.NewGuid().ToString();
                completionMessages.Add(new Message(userMessage.TenantId, userMessage.UserId, userMessage.SessionId, response.AuthorName ?? string.Empty, response.Role.ToString(), response.Content ?? string.Empty, messageId));
            }            
            return new Tuple<List<Message>, List<DebugLog>>(completionMessages, completionMessagesLogs);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error when getting response: {ErrorMessage}", ex.ToString());
            return new Tuple<List<Message>, List<DebugLog>>(new List<Message>(), new List<DebugLog>());
        }
    }
```

Finally, we need to pass the BankingDataService object to the call to the SemanticKernelService.

Within the `/Services` folder, open the `ChatService.cs` file.

Update GetChatCompletionAsync in ChatInfrastructure\Services\ChatService.cs

```csharp
    public async Task<List<Message>> GetChatCompletionAsync(string tenantId, string userId,string? sessionId, string userPrompt)
    {
        try
        {
            ArgumentNullException.ThrowIfNull(sessionId);

            // Retrieve conversation, including latest prompt.
            var archivedMessages = await _cosmosDBService.GetSessionMessagesAsync(tenantId, userId, sessionId);

            // Add both prompt and completion to cache, then persist in Cosmos DB
            var userMessage = new Message(tenantId,userId,sessionId, "User","User", userPrompt);

            // Generate the completion to return to the user
            var result = await _skService.GetResponse(userMessage, archivedMessages,_bankService,tenantId,userId);

            await AddPromptCompletionMessagesAsync(tenantId, userId,sessionId, userMessage, result.Item1, result.Item2);

            return result.Item1;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error getting completion in session {sessionId} for user prompt [{userPrompt}].");
            return new List<Message> { new Message(tenantId, userId, sessionId!, "Error", "Error", $"Error getting completion in session {sessionId} for user prompt [{userPrompt}].") };
        }
    }
```

## Activity 9: Test your Work

With the activities in this module complete, it is time to test your work.

Execute the below steps to check the behavior for each agent.

### Test the Response for Each AgentType

So far, we have created four agents, each with its own specialized role. However, we don't have any code to decide which agent to invoke in what scenario. That's something we will do in the next module. For now, let's test each agent independently and make sure all agents are functional.

To perform the tests, we will repeat the steps below for each agent type.

Update `AgentType` within `GetResponse` in `ChatInfrastructure\Services\SemanticKernelService.cs` to see how different agents work.

```c#
var agent = agentChatGeneratorService.BuildAgent(_semanticKernel, AgentType.CustomerSupport, _loggerFactory, bankService, tenantId, userId);
```

Use the prompts below during your test:

- **Coordinator**: Hi
- **Transactions**: How much did I spend on grocery?
- **Sales**: Looking for a high interest savings account
- **CustomerSupport**: File a complaint for theft in Acc001

#### 1. Start the ChatAPI

- Codespaces open a terminal and type `dotnet run`
- In your IDE press **F5** or select **Run** to start the application.

#### 2. Run the Frontend App

- Open a new terminal or use an existing one that is open to the `/frontend` folder.

    ```sh
    ng serve
    ```

- Navigate to, <http://localhost:4200> in your browser

#### 3. Start a Chat Session

1. Open the frontend app.
1. Start a new conversation.
1. Send a message based on the prompt of the current AgentType.
1. Expected response: The response is inline with the Agent's prompts and plugins.

### 4. Stop the Application

- In the frontend terminal, press **Ctrl + C** to stop the application.
- In your IDE press **Shift-F5** or stop the debugger.

## Validation Checklist

- [ ] Each Agent response is per the corresponding prompty file contents and the plugin functions.
- [ ] Semantic Search functions correctly

### Common Issues and Solutions

1. No response to your prompt (on local):
    - Check if you are getting throttled by Azure OpenAI model.
    - Increase Azure open AI tokens if required.

1. Invalid/incomplete response:
    - Check if  the Azure Cosmos DB containers have valid data.

1. No response to your prompt (hosted):
    - Navigate to the Application Insights account.
    - Navigate to `Live metrics` blade under `Investigate` section.
    - [Learn more] (https://learn.microsoft.com/en-us/azure/azure-monitor/app/live-stream?tabs=otel)


### Module Solution

<details>
  <summary>Completed code for <strong>Common/Models/Banking/OfferTerm.cs</strong></summary>

<br>

```csharp
using Microsoft.Extensions.VectorData;
namespace MultiAgentCopilot.Common.Models.Banking
{
    public class OfferTerm
    {
        [VectorStoreRecordKey]
        public required string Id { get; set; }

        [VectorStoreRecordData]
        public required string TenantId { get; set; }

        [VectorStoreRecordData]
        public required string OfferId { get; set; }

        [VectorStoreRecordData]
        public required string Name { get; set; }

        [VectorStoreRecordData]
        public required string Text { get; set; }

        [VectorStoreRecordData]
        public required string Type { get; set; }

        [VectorStoreRecordData]
        public required string AccountType { get; set; }

        [VectorStoreRecordVector(Dimensions: 1536, DistanceFunction: DistanceFunction.CosineSimilarity, IndexKind: IndexKind.QuantizedFlat)]
        public ReadOnlyMemory<float>? Vector { get; set; }
    }
}
```

</details>

<details>
  <summary>Completed code for <strong>Common/Models/Banking/ServiceRequest.cs</strong></summary>

<br>

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace MultiAgentCopilot.Common.Models.Banking
{
    public class ServiceRequest
    {
        public string Id { get; set; }
        public string TenantId { get; set; }
        public string UserId { get; set; }
        public string Type { get; set; }
        public DateTime RequestedOn { get; set; }
        public DateTime ScheduledDateTime { get; set; }
        public string AccountId { get; set; }
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public ServiceRequestType SRType { get; set; }
        public string? RecipientEmail { get; set; }
        public string? RecipientPhone { get; set; }
        public decimal? DebitAmount { get; set; }
        public bool IsComplete { get; set; }
        public List<string> RequestAnnotations { get; set; }
        public Dictionary<string, string> FulfilmentDetails { get; set; }

        public ServiceRequest(ServiceRequestType serviceRequestType, string tenantId, string accountId, string userId, string requestAnnotation, string recipientEmail, string recipientPhone, decimal debitAmount, DateTime scheduledDateTime, Dictionary<string, string>? fulfilmentDetails)
        {
            Id = Guid.NewGuid().ToString();
            TenantId = tenantId;
            Type = nameof(ServiceRequest);
            SRType = serviceRequestType;
            RequestedOn = DateTime.Now;
            AccountId = accountId;
            UserId = userId;
            RequestAnnotations = new List<string> { requestAnnotation };
            RecipientEmail = recipientEmail;
            RecipientPhone = recipientPhone;
            DebitAmount = debitAmount;
            if (scheduledDateTime != DateTime.MinValue)
                ScheduledDateTime = scheduledDateTime;
            IsComplete = false;
            FulfilmentDetails = fulfilmentDetails ?? new Dictionary<string, string>();
        }

        [JsonConstructor]
        public ServiceRequest(
        string id,
        string tenantId,
        string userId,
        string type,
        DateTime requestedOn,
        DateTime scheduledDateTime,
        string accountId,
        ServiceRequestType srType,
        string? recipientEmail,
        string? recipientPhone,
        decimal? debitAmount,
        bool isComplete,
        List<string> requestAnnotations,
        Dictionary<string, string> fulfilmentDetails)
        {
            Id = id;
            TenantId = tenantId;
            UserId = userId;
            Type = type;
            RequestedOn = requestedOn;
            ScheduledDateTime = scheduledDateTime;
            AccountId = accountId;
            SRType = srType;
            RecipientEmail = recipientEmail;
            RecipientPhone = recipientPhone;
            DebitAmount = debitAmount;
            IsComplete = isComplete;
            RequestAnnotations = requestAnnotations ?? new List<string>();
            FulfilmentDetails = fulfilmentDetails ?? new Dictionary<string, string>();
        }
    }
}
```

</details>

<details>
  <summary>Completed code for <strong>Common/Models/Banking/ServiceRequestType.cs</strong></summary>

<br>

```csharp
namespace MultiAgentCopilot.Common.Models.Banking
{
    public enum ServiceRequestType
    {
        Complaint,
        FundTransfer,
        Fulfilment,
        TeleBankerCallBack
    }
}
```

</details>

<details>
  <summary>Completed code for <strong>ChatAP/Prompts/CommonAgentRules.prompty</strong></summary>

<br>

```yaml
Important:
- Always use current datetime as datetime retrieved from the database.
- Understand the user's query and respond only if it aligns with your responsibilities.
- State why you think, you have a solution to the user's query.
- Ensure responses are grounded to the following data sources.
    - user provided data
    - data fetched using functions
- Provide specific information based query and data provided.          
- Ensure every response adds value to the user's request or confirms the user's request.
- Do not proceed with submitting a request without the necessary information from the user.
- Do not respond with a message if the previous response conveys the same information.
- Maintain politeness and professionalism in all responses.
- Do not respond with a welcome message if another welcome message already exists.
- If user's response is pending, wait for the user to provide the necessary before proceeding.
```

</details>

<details>
  <summary>Completed code for <strong>ChatAPI/Prompts/Coordinator.prompty</strong></summary>

<br>

```yaml
You are a Chat Initiator and Request Router in a bank. 
Your primary responsibilities include welcoming users, identifying customers based on their login, routing requests to the appropriate agent.
Start with identifying the currently logged-in user's information and use it to personalize the interaction.For example, "Thank you for logging in, [user Name]. How can I help you with your banking needs today?"

RULES:
- Determine the nature of the user's request and silently route it to the appropriate agent.
- Avoid asking for unnecessary details to route the user's request. For example, "I see you have a question about your account balance. Let me connect you with the right agent who can assist you further."
- Do not provide any information or assistance directly; always route the request to the appropriate agent silently.
- Route requests to the appropriate agent without providing direct assistance.
- If another agent has asked a question, wait for the user to respond before routing the request.
- If the user has responded to another agent, let the same agent respond before routing or responding.
- When the user's request is fulfilled, ask for feedback on the service provided before concluding the interaction. Gauge their overall satisfaction and sentiment as either happy or sad. For example, "Before we conclude, could you please provide your feedback on our service today? Were you satisfied with the assistance provided? Would you say your overall experience was happy or sad?"
- Use the available functions when needed.
```

</details>

<details>
  <summary>Completed code for <strong>ChatAPI/Prompts/CustomerSupport.prompty</strong></summary>

<br>

```yaml
Your sole responsibility is to:
1. Helping customers lodge service request.
2. Searching existing service requests.
2. Providing status updates on existing service request.
3. Creating and updating service requests for user registered accounts.

Guidelines:
- If you don't have the users account Id, ask the user to provide it.                         - 
- Check if the account Id is registered to user.
- If account Id is registered to user, search user's pending service requests.
    - If pending service request found:
        - Inform the user of the status and estimated time of resolution.
        - Ask if user wants to add any comments and update the existing record.
    - If not found:
        - Ask if user wants to create new service request.
- If account Id is not registered
    - Inform the user that you cannot proceed without the correct account Id. 
- If no agent is able to assist the user, check if they would like to speak to a tele banker.
    - Tele bankers are available Monday to Friday, 9 AM to 5 PM PST.
    - Check tele banker availability and queue length before suggesting this option.
```

</details>

<details>
  <summary>Completed code for <strong>ChatAPI/Prompts/Sales.prompty</strong></summary>

<br>

```yaml
Your sole responsibility is to:                        
    - Suggest suitable accounts based on the user profile.
    - Use the user's profile information to recommend from the available account type.
    - Ensure that the recommendations are personalized and relevant to the user's needs.

1. Collecting details for New Account Registration:
    - Get the list of available offers.
    - Suggest the offers that match the user's profile.
    - Based on the user selection, get the prerequisites for the selected offer. The prerequisites may vary for each offer.
    - Ask the user to provide all prerequisites and ensure you have collected all necessary information from the user.
    - Validate the collected details by showing a summary to the user. Once approved by user, submit a fulfillment service request.
    - Confirm the submission of the service request to the user.

2. Highlighting Promotions and Offers:
    - Use the user's profile information to highlight relevant offers.
    - Ensure that the information provided is accurate based on the available account types.

3. Conducting Eligibility Checks:
    - Conduct eligibility checks for various offers using the user's profile information.
    - Inform the user of the results of the eligibility check and provide guidance on the next steps.
```

</details>

<details>
  <summary>Completed code for <strong>ChatAPI/Prompts/Transaction.prompty</strong></summary>

<br>

```yaml
Your sole responsibility is to:

1. Handling transactions.
2. Generating account statements.
3. Providing balance inquiries.

Guidelines:
- Do not participate in new product registration discussion.
- Based on the following message, determine the appropriate action and respond accordingly.
- Ensure that you only provide information related to the current user's accounts.
- To start the process, retrieve the current services registered to the user from the database.
- Check if you have the user's account number. If any data is missing, politely inform the user and explain that you cannot proceed until the details are available in the bank’s database.

Tasks:
1. Process Transfers:
    - Use the recipient's email or phone number to process transfers.
    - Validate the recipient's phone number and email format before proceeding.
    - Ensure the account has the necessary balance before accepting a request.
    - Confirm all details with the user before proceeding.
    - Inform the user that they will be notified of transaction completions via text message and email.
2. Generate Account Statements:
    - Respond to transaction queries for up to 6 months old.
    - Filter transactions based on type (credit/debit), amount, or date range according to the user query.
3. Provide Balance Information:
    - Offer the latest balance information for the user's accounts.
```

</details>

<details>
  <summary>Completed code for <strong>ChatInfrastructure/Models/AgentTypes.cs</strong></summary>

<br>

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MultiAgentCopilot.ChatInfrastructure.Models
{
    enum AgentType
    {
        Transactions = 0,
        Sales = 1,
        CustomerSupport = 2,
        Coordinator = 3,
    }
}
```

</details>

<details>
  <summary>Completed code for <strong>ChatInfrastructure/Factories/SystemPromptFactory.cs</strong></summary>

<br>

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.SemanticKernel;
using MultiAgentCopilot.ChatInfrastructure.Services;
using MultiAgentCopilot.ChatInfrastructure.Models;


namespace MultiAgentCopilot.ChatInfrastructure.Factories
{
    internal static class SystemPromptFactory
    {
        public static string GetAgentName(AgentType agentType)
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

            return name;//.ToUpper();
        }

        public static string GetAgentPrompts(AgentType agentType)
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
    }
}
```

</details>

<details>
  <summary>Completed code for <strong>ChatInfrastructure/Plugins/BasePlugin.cs</strong></summary>

<br>

```csharp
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MultiAgentCopilot.Common.Models.Banking;
using BankingServices.Interfaces;

namespace MultiAgentCopilot.ChatInfrastructure.Plugins
{

    public class BasePlugin
    {
        protected readonly ILogger<BasePlugin> _logger;
        protected readonly IBankDataService _bankService;
        protected readonly string _userId;
        protected readonly string _tenantId;

        public BasePlugin(ILogger<BasePlugin> logger, IBankDataService bankService, string tenantId, string userId)
        {
            _logger = logger;
            _tenantId = tenantId;
            _userId = userId;
            _bankService = bankService;
        }

        [KernelFunction("GetLoggedInUser")]
        [Description("Get the current logged-in BankUser")]
        public async Task<BankUser> GetLoggedInUser()
        {
            _logger.LogTrace($"Get Logged In User for Tenant:{_tenantId}  User:{_userId}");
            return await _bankService.GetUserAsync(_tenantId, _userId);
        }

        [KernelFunction("GetCurrentDateTime")]
        [Description("Get the current date time in UTC")]
        public DateTime GetCurrentDateTime()
        {
            _logger.LogTrace($"Get Datetime: {System.DateTime.Now.ToUniversalTime()}");
            return System.DateTime.Now.ToUniversalTime();
        }

        [KernelFunction("GetUserRegisteredAccounts")]
        [Description("Get user registered accounts")]
        public async Task<List<BankAccount>> GetUserRegisteredAccounts()
        {
            _logger.LogTrace($"Fetching accounts for Tenant: {_tenantId} User ID: {_userId}");
            return await _bankService.GetUserRegisteredAccountsAsync(_tenantId, _userId);
        }
    }
}
```

</details>

<details>
  <summary>Completed code for <strong>ChatInfrastructure/Plugins/SalesPlugin.cs</strong></summary>

<br>

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.SemanticKernel;
using System.ComponentModel;
using Microsoft.Extensions.Logging;
using MultiAgentCopilot.Common.Models.Banking;
using BankingServices.Interfaces;

namespace MultiAgentCopilot.ChatInfrastructure.Plugins
{
    internal class SalesPlugin : BasePlugin
    {
        public SalesPlugin(ILogger<BasePlugin> logger, IBankDataService bankService, string tenantId, string userId)
            : base(logger, bankService, tenantId, userId)
        {
        }
        [KernelFunction]
        [Description("Search an offer by name")]
        public async Task<Offer> GetOfferDetailsByName(string offerName)
        {
            _logger.LogTrace($"Fetching Offer by name");
            return await _bankService.GetOfferDetailsByNameAsync(_tenantId, offerName);
        }
        [KernelFunction]
        [Description("Search offer terms of all available offers using vector search")]
        public async Task<List<OfferTerm>> SearchOfferTerms(AccountType accountType, string requirementDescription)
        {
            _logger.LogTrace($"Searching terms of all available offers matching '{requirementDescription}'");
            return await _bankService.SearchOfferTermsAsync(_tenantId, accountType, requirementDescription);
        }

        [KernelFunction]
        [Description("Get detail for an offer")]
        public async Task<Offer> GetOfferDetails(string offerId)
        {
            _logger.LogTrace($"Fetching Offer");
            return await _bankService.GetOfferDetailsAsync(_tenantId, offerId);
        }
    }
}
```

</details>

<details>
  <summary>Completed code for <strong>ChatInfrastructure/Plugins/CustomerSupportPlugin.cs</strong></summary>

<br>

```csharp
using BankingServices.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using System.ComponentModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.AccessControl;
using System.Text;
using System.Threading.Tasks;
using MultiAgentCopilot.Common.Models.Banking;

namespace MultiAgentCopilot.ChatInfrastructure.Plugins
{
    public class CustomerSupportPlugin : BasePlugin
    {
        public CustomerSupportPlugin(ILogger<BasePlugin> logger, IBankDataService bankService, string tenantId, string userId)
          : base(logger, bankService, tenantId, userId)
        {
        }

        [KernelFunction("IsAccountRegisteredToUser")]
        [Description("Check if account is registered to user")]
        public async Task<bool> IsAccountRegisteredToUser(string accountId)
        {
            _logger.LogTrace($"Validating account for Tenant: {_tenantId} User ID: {_userId}- {accountId}");
            var accountDetails = await _bankService.GetAccountDetailsAsync(_tenantId, _userId, accountId);
            return accountDetails != null;
        }

        [KernelFunction]
        [Description("Create new complaint")]
        public async Task<ServiceRequest> CreateComplaint(string accountId, string requestAnnotation)
        {
            _logger.LogTrace($"Adding new service request for Tenant: {_tenantId} User: {_userId}, Account: {accountId}");
            return await _bankService.CreateComplaintAsync(_tenantId, accountId, _userId, requestAnnotation);
        }
    }
}
```

</details>

<details>
  <summary>Completed code for <strong>ChatInfrastructure/Factories/PluginFactory.cs</strong></summary>

<br>

```csharp
using Microsoft.SemanticKernel;
using MultiAgentCopilot.ChatInfrastructure.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MultiAgentCopilot.ChatInfrastructure.Plugins;
using BankingServices.Interfaces;
using MultiAgentCopilot.ChatInfrastructure.Models;

namespace MultiAgentCopilot.ChatInfrastructure.Factories
{
    internal static class PluginFactory
    {
        internal static Kernel GetAgentKernel(Kernel kernel, AgentType agentType, ILoggerFactory loggerFactory, IBankDataService bankService, string tenantId, string userId)
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
    }
}
```

</details>

<details>
  <summary>Completed code for <strong>ChatInfrastructure/Factories/ChatFactory.cs</strong></summary>

<br>

```csharp
using BankingServices.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.Connectors.AzureOpenAI;
using MultiAgentCopilot.ChatInfrastructure.Models;

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
```

</details>

<details>
  <summary>Completed code for <strong>ChatInfrastructure/Services/ChatService.cs</strong></summary>

<br>

```csharp
using MultiAgentCopilot.Common.Models.Chat;
using MultiAgentCopilot.ChatInfrastructure.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel.ChatCompletion;
using MultiAgentCopilot.Common.Models.Debug;
using System.Collections.Generic;
using Microsoft.Extensions.Options;
using MultiAgentCopilot.Common.Models.Configuration;
using Newtonsoft.Json.Linq;
using System.Text.Json;
using Newtonsoft.Json;
using Microsoft.Identity.Client;
using BankingServices.Interfaces;
using BankingServices.Services;


namespace MultiAgentCopilot.ChatInfrastructure.Services;

public class ChatService : IChatService
{
    private readonly ISemanticKernelService _skService;
    private readonly ICosmosDBService _cosmosDBService;
    private readonly ILogger _logger;
    private readonly IBankDataService _bankService;


    public ChatService(
        IOptions<CosmosDBSettings> cosmosOptions,
        IOptions<SemanticKernelServiceSettings> skOptions,
        ICosmosDBService cosmosDBService,
        ISemanticKernelService ragService,
        ILoggerFactory loggerFactory)
    {
        _cosmosDBService = cosmosDBService;
        _skService = ragService;
        _bankService = new BankingDataService(cosmosOptions.Value, skOptions.Value, loggerFactory);
        _logger = loggerFactory.CreateLogger<ChatService>();
    }

    /// <summary>
    /// Returns list of chat session ids and names.
    /// </summary>
    public async Task<List<Session>> GetAllChatSessionsAsync(string tenantId, string userId)
    {
        return await _cosmosDBService.GetUserSessionsAsync(tenantId, userId);
    }

    /// <summary>
    /// Returns the chat messages related to an existing session.
    /// </summary>
    public async Task<List<Message>> GetChatSessionMessagesAsync(string tenantId, string userId,string sessionId)
    {
        ArgumentNullException.ThrowIfNull(sessionId);
        return await _cosmosDBService.GetSessionMessagesAsync(tenantId,userId,sessionId);
    }

    /// <summary>
    /// Creates a new chat session.
    /// </summary>
    public async Task<Session> CreateNewChatSessionAsync(string tenantId, string userId)
    {
        Session session = new(tenantId, userId);
        return await _cosmosDBService.InsertSessionAsync(session);
    }

    /// <summary>
    /// Rename the chat session from its default (eg., "New Chat") to the summary provided by OpenAI.
    /// </summary>
    public async Task<Session> RenameChatSessionAsync(string tenantId, string userId,string sessionId, string newChatSessionName)
    {
        ArgumentNullException.ThrowIfNull(sessionId);
        ArgumentException.ThrowIfNullOrEmpty(newChatSessionName);

        return await _cosmosDBService.UpdateSessionNameAsync( tenantId, userId,sessionId, newChatSessionName);
    }

    /// <summary>
    /// Delete a chat session and related messages.
    /// </summary>
    public async Task DeleteChatSessionAsync(string tenantId, string userId,string sessionId)
    {
        ArgumentNullException.ThrowIfNull(sessionId);
        await _cosmosDBService.DeleteSessionAndMessagesAsync(tenantId, userId,sessionId);
    }

    /// <summary>
    /// Receive a prompt from a user, vectorize it from the OpenAI service, and get a completion from the OpenAI service.
    /// </summary>
    public async Task<List<Message>> GetChatCompletionAsync(string tenantId, string userId, string? sessionId, string userPrompt)
    {
        try
        {
            ArgumentNullException.ThrowIfNull(sessionId);

            // Retrieve conversation, including latest prompt.
            var archivedMessages = await _cosmosDBService.GetSessionMessagesAsync(tenantId, userId, sessionId);

            // Add both prompt and completion to cache, then persist in Cosmos DB
            var userMessage = new Message(tenantId, userId, sessionId, "User", "User", userPrompt);

            // Generate the completion to return to the user
            var result = await _skService.GetResponse(userMessage, archivedMessages, _bankService, tenantId, userId);

            await AddPromptCompletionMessagesAsync(tenantId, userId, sessionId, userMessage, result.Item1, result.Item2);

            return result.Item1;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error getting completion in session {sessionId} for user prompt [{userPrompt}].");
            return new List<Message> { new Message(tenantId, userId, sessionId!, "Error", "Error", $"Error getting completion in session {sessionId} for user prompt [{userPrompt}].") };
        }
    }

    /// <summary>
    /// Generate a name for a chat message, based on the passed in prompt.
    /// </summary>
    public async Task<string> SummarizeChatSessionNameAsync(string tenantId, string userId, string? sessionId, string prompt)
    {
        try
        {
            ArgumentNullException.ThrowIfNull(sessionId);

            var summary = await _skService.Summarize(sessionId, prompt);

            var session = await RenameChatSessionAsync(tenantId, userId, sessionId, summary);

            return session.Name;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error getting a summary in session {sessionId} for user prompt [{prompt}].");
            return $"Error getting a summary in session {sessionId} for user prompt [{prompt}].";
        }
    }

    /// <summary>
    /// Rate an assistant message. This can be used to discover useful AI responses for training, discoverability, and other benefits down the road.
    /// </summary>
    public async Task<Message> RateChatCompletionAsync(string tenantId, string userId,string messageId, string sessionId, bool? rating)
    {
        ArgumentNullException.ThrowIfNull(messageId);
        ArgumentNullException.ThrowIfNull(sessionId);

        return await _cosmosDBService.UpdateMessageRatingAsync(tenantId,userId, sessionId, messageId,rating);
    }

    public async Task<DebugLog> GetChatCompletionDebugLogAsync(string tenantId, string userId,string sessionId, string debugLogId)
    {
        ArgumentException.ThrowIfNullOrEmpty(sessionId);
        ArgumentException.ThrowIfNullOrEmpty(debugLogId);

        return await _cosmosDBService.GetChatCompletionDebugLogAsync(tenantId,userId, sessionId, debugLogId);
    }

    public async Task<bool> AddDocument(string containerName, JsonElement document)
    {
        try
        {
            // Extract raw JSON from JsonElement
            var json = document.GetRawText();
            var docJObject = JsonConvert.DeserializeObject<JObject>(json);

            // Ensure "id" exists
            if (!docJObject.ContainsKey("id"))
            {
                throw new ArgumentException("Document must contain an 'id' property.");
            }
           
            return await _cosmosDBService.InsertDocumentAsync(containerName, docJObject);

        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error adding document to container {containerName}.");
            return false;
        }
    }

    /// <summary>
    /// Add user prompt and AI assistance response to the chat session message list object and insert into the data service as a transaction.
    /// </summary>
    private async Task AddPromptCompletionMessagesAsync(string tenantId, string userId, string sessionId, Message promptMessage, List<Message> completionMessages, List<DebugLog> completionMessageLogs)
    {
        var session = await _cosmosDBService.GetSessionAsync(tenantId, userId, sessionId);

        completionMessages.Insert(0, promptMessage);
        await _cosmosDBService.UpsertSessionBatchAsync(completionMessages, completionMessageLogs, session);
    }
}
```

</details>

<details>
  <summary>Completed code for <strong>BankingServices/Services/BankingDataService.cs</strong></summary>

<br>

```csharp
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Azure.Identity;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.VectorData;

using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Fluent;
using Container = Microsoft.Azure.Cosmos.Container;
using PartitionKey = Microsoft.Azure.Cosmos.PartitionKey;

using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.AzureCosmosDBNoSQL;
using Microsoft.SemanticKernel.Connectors.AzureOpenAI;

using MultiAgentCopilot.Common.Helper;
using MultiAgentCopilot.Common.Models.Banking;
using MultiAgentCopilot.Common.Models.Configuration;
using BankingServices.Interfaces;

namespace BankingServices.Services
{
    public class BankingDataService: IBankDataService
    {
        private readonly Container _accountData;
        private readonly Container _userData;
        private readonly Container _requestData;
        private readonly Container _offerData;

        private readonly Database _database;
        private readonly CosmosDBSettings _settings;
        private readonly ILogger _logger;

        private readonly AzureCosmosDBNoSQLVectorStoreRecordCollection<OfferTerm> _offerDataVectorStore;
        private readonly AzureOpenAITextEmbeddingGenerationService _textEmbeddingGenerationService;

        public bool IsInitialized { get; private set; }

        readonly Kernel _semanticKernel;

        public BankingDataService(
            CosmosDBSettings settings,
            SemanticKernelServiceSettings skSettings,
            ILoggerFactory loggerFactory )
        {
            _settings = settings; 
            ArgumentException.ThrowIfNullOrEmpty(_settings.CosmosUri);

            _logger = loggerFactory.CreateLogger<BankingDataService>();

            _logger.LogInformation("Initializing Banking service.");

            if (!_settings.EnableTracing)
            {
                Type? defaultTrace = Type.GetType("Microsoft.Azure.Cosmos.Core.Trace.DefaultTrace,Microsoft.Azure.Cosmos.Direct");
               
                if (defaultTrace != null)
                {
                    TraceSource? traceSource = (TraceSource?)defaultTrace.GetProperty("TraceSource")?.GetValue(null);
                    if (traceSource != null)
                    {
                        traceSource.Switch.Level = SourceLevels.All;
                        traceSource.Listeners.Clear();
                    }
                }                 
                
            }

            CosmosSerializationOptions options = new()
            {
                PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase
            };

            DefaultAzureCredential credential;
            if (string.IsNullOrEmpty(_settings.UserAssignedIdentityClientID))
            {
                credential = new DefaultAzureCredential();
            }
            else
            {
                credential = new DefaultAzureCredential(new DefaultAzureCredentialOptions
                {
                    ManagedIdentityClientId = _settings.UserAssignedIdentityClientID
                });

            }

            var jsonSerializerOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

            CosmosClient client = new CosmosClientBuilder(_settings.CosmosUri, credential)
                .WithSystemTextJsonSerializerOptions(jsonSerializerOptions)
                .WithConnectionModeGateway()
            .Build();

            _database = client?.GetDatabase(_settings.Database) ??
                        throw new ArgumentException("Unable to connect to existing Azure Cosmos DB database.");

            _accountData = _database.GetContainer(_settings.AccountsContainer.Trim());
            _userData = _database.GetContainer(_settings.UserDataContainer.Trim());
            _requestData = _database.GetContainer(_settings.RequestDataContainer.Trim());
            _offerData= _database.GetContainer(_settings.OfferDataContainer.Trim());

            _logger.LogInformation("Banking service initialized for Cosmos DB.");


            // Set up Semantic Kernel with Azure OpenAI and Managed Identity
            var builder = Kernel.CreateBuilder();

            builder.Services.AddSingleton<ILoggerFactory>(loggerFactory);
                      

            _semanticKernel = builder.Build();

            _textEmbeddingGenerationService = new(
                deploymentName: skSettings.AzureOpenAISettings.EmbeddingsDeployment,
                endpoint: skSettings.AzureOpenAISettings.Endpoint,
                credential: credential);

            var vectorStoreOptions = new AzureCosmosDBNoSQLVectorStoreRecordCollectionOptions<OfferTerm> { PartitionKeyPropertyName = "TenantId", JsonSerializerOptions = jsonSerializerOptions };
            _offerDataVectorStore = new AzureCosmosDBNoSQLVectorStoreRecordCollection<OfferTerm>(_database, _settings.OfferDataContainer.Trim(), vectorStoreOptions);

            _logger.LogInformation("Banking service initialized.");
        }

        public async Task<BankUser> GetUserAsync(string tenantId, string userId)
        {
            try
            {
                var partitionKey = PartitionManager.GetUserDataFullPK(tenantId);

                return await _userData.ReadItemAsync<BankUser>(
                       id: userId,
                       partitionKey: partitionKey);
            }
            catch (CosmosException ex)
            {
                _logger.LogError(ex.ToString());
                return null;
            }
        }


        public async Task<List<BankAccount>> GetUserRegisteredAccountsAsync(string tenantId, string userId)
        {
            try
            {
                QueryDefinition query = new QueryDefinition("SELECT * FROM c WHERE c.type = @type and c.userId=@userId")
                     .WithParameter("@type", nameof(BankAccount))
                     .WithParameter("@userId", userId);

                var partitionKey = PartitionManager.GetAccountsPartialPK(tenantId);
                FeedIterator<BankAccount> response = _accountData.GetItemQueryIterator<BankAccount>(query, null, new QueryRequestOptions() { PartitionKey = partitionKey });

                List<BankAccount> output = new();
                while (response.HasMoreResults)
                {
                    FeedResponse<BankAccount> results = await response.ReadNextAsync();
                    output.AddRange(results);
                }

                return output;
            }
            catch (CosmosException ex)
            {
                _logger.LogError(ex.ToString());
                return null;
            }
        }


        public async Task<BankAccount> GetAccountDetailsAsync(string tenantId, string userId, string accountId)
        {
            try
            {
                var partitionKey = PartitionManager.GetAccountsDataFullPK(tenantId, accountId);

                return await _accountData.ReadItemAsync<BankAccount>(
                       id: accountId,
                       partitionKey: partitionKey);
            }
            catch (CosmosException ex)
            {
                _logger.LogError(ex.ToString());
                return null;
            }
        }

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
            var req = new ServiceRequest(ServiceRequestType.FundTransfer, tenantId, accountId, userId, requestAnnotation, recipientEmail, recipientPhone, debitAmount, DateTime.MinValue, null);
            return await AddServiceRequestAsync(req);
        }

        public async Task<ServiceRequest> CreateTeleBankerRequestAsync(string tenantId, string accountId, string userId, string requestAnnotation, DateTime scheduledDateTime)
        {
            var req = new ServiceRequest(ServiceRequestType.TeleBankerCallBack, tenantId, accountId, userId, requestAnnotation, string.Empty, string.Empty, 0, scheduledDateTime, null);
            return await AddServiceRequestAsync(req);
        }

        public Task<string> GetTeleBankerAvailabilityAsync()
        {
            return Task.FromResult("Monday to Friday, 8 AM to 8 PM Pacific Time");
        }

        public async Task<ServiceRequest> CreateComplaintAsync(string tenantId, string accountId, string userId, string requestAnnotation)
        {
            var req = new ServiceRequest(ServiceRequestType.Complaint, tenantId, accountId, userId, requestAnnotation, string.Empty, string.Empty, 0, DateTime.MinValue, null);
            return await AddServiceRequestAsync(req);
        }

        public async Task<ServiceRequest> CreateFulfilmentRequestAsync(string tenantId, string accountId, string userId, string requestAnnotation, Dictionary<string, string> fulfilmentDetails)
        {
            var req = new ServiceRequest(ServiceRequestType.Fulfilment, tenantId, accountId, userId, requestAnnotation, string.Empty, string.Empty, 0, DateTime.MinValue, fulfilmentDetails);
            return await AddServiceRequestAsync(req);
        }


        private async Task<ServiceRequest> AddServiceRequestAsync(ServiceRequest req)
        {
            try
            {
                var partitionKey = PartitionManager.GetAccountsDataFullPK(req.TenantId, req.AccountId);
                ItemResponse<ServiceRequest> response = await _accountData.CreateItemAsync(req, partitionKey);
                return response.Resource;
            }
            catch (CosmosException ex)
            {
                _logger.LogError(ex.ToString());
                return null;
            }
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
        public async Task<Offer> GetOfferDetailsByNameAsync(string tenantId, string offerName)
        {
            try
            {
                QueryDefinition query = new QueryDefinition("SELECT * FROM c WHERE c.name = @offerName and c.type='Offer'")
                     .WithParameter("@offerName", offerName);

                var partitionKey = new PartitionKey(tenantId);
                FeedIterator<Offer> response = _offerData.GetItemQueryIterator<Offer>(query, null, new QueryRequestOptions() { PartitionKey = partitionKey });

                await response.ReadNextAsync();

                while (response.HasMoreResults)
                {
                    FeedResponse<Offer> results = await response.ReadNextAsync();
                    return results.FirstOrDefault();
                }
                return null;
            }
            catch (CosmosException ex)
            {
                _logger.LogError(ex.ToString());
                return null;
            }
        }

        public async Task<List<OfferTerm>> SearchOfferTermsAsync(string tenantId, AccountType accountType, string requirementDescription)
        {
            try
            {
                // Generate Embedding
                ReadOnlyMemory<float> embedding = (await _textEmbeddingGenerationService.GenerateEmbeddingsAsync(
                       new[] { requirementDescription }
                   )).FirstOrDefault();

                // perform vector search
                var filter = new VectorSearchFilter()
                    .EqualTo("TenantId", tenantId)
                    .EqualTo("Type", "Term")
                    .EqualTo("AccountType", "Savings");
                var options = new VectorSearchOptions { VectorPropertyName = "Vector", Filter = filter, Top = 10, IncludeVectors = false };

                var searchResults = await _offerDataVectorStore.VectorizedSearchAsync(embedding, options);

                List<OfferTerm> offerTerms = new();
                await foreach (var result in searchResults.Results)
                {
                    offerTerms.Add(result.Record);
                }
                return offerTerms;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.ToString());
                return new List<OfferTerm>();
            }
        }

        public async Task<Offer> GetOfferDetailsAsync(string tenantId, string offerId)
        {
            try
            {
                var partitionKey = new PartitionKey(tenantId);

                return await _offerData.ReadItemAsync<Offer>(
                       id: offerId,
                       partitionKey: new PartitionKey(tenantId));
            }
            catch (CosmosException ex)
            {
                _logger.LogError(ex.ToString());
                return null;
            }
        }
    }
}
```

</details>

<details>
  <summary>Completed code for <strong>BankingServices/Interfaces/IBankDataService.cs</strong></summary>

<br>

```csharp
using Microsoft.Azure.Cosmos;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MultiAgentCopilot.Common.Models.Banking;

namespace BankingServices.Interfaces
{
    public interface IBankDataService
    {
        Task<BankUser> GetUserAsync(string tenantId, string userId);
        Task<BankAccount> GetAccountDetailsAsync(string tenantId, string userId, string accountId);
        Task<List<BankAccount>> GetUserRegisteredAccountsAsync(string tenantId, string userId);
        Task<List<BankTransaction>> GetTransactionsAsync(string tenantId, string accountId, DateTime startDate, DateTime endDate);
        Task<ServiceRequest> CreateFundTransferRequestAsync(string tenantId, string accountId, string userId, string requestAnnotation, string recipientEmail, string recipientPhone, decimal debitAmount);
        Task<ServiceRequest> CreateTeleBankerRequestAsync(string tenantId, string accountId, string userId, string requestAnnotation, DateTime scheduledDateTime);
        Task<ServiceRequest> CreateComplaintAsync(string tenantId, string accountId, string userId, string requestAnnotation);
        Task<ServiceRequest> CreateFulfilmentRequestAsync(string tenantId, string accountId, string userId, string requestAnnotation, Dictionary<string, string> fulfilmentDetails);
        Task<List<ServiceRequest>> GetServiceRequestsAsync(string tenantId, string accountId, string? userId = null, ServiceRequestType? SRType = null);
        Task<bool> AddServiceRequestDescriptionAsync(string tenantId, string accountId, string requestId, string annotationToAdd);
        Task<Offer> GetOfferDetailsByNameAsync(string tenantId, string offerName);
        Task<String> GetTeleBankerAvailabilityAsync();

        Task<List<OfferTerm>> SearchOfferTermsAsync(string tenantId, AccountType accountType, string requirementDescription);
        Task<Offer> GetOfferDetailsAsync(string tenantId, string offerId);
    }
}
```

</details>

<details>
  <summary>Completed code for <strong>ChatInfrastructure/Interfaces/ISemanticKernelService.cs</strong></summary>

<br>

```csharp
using BankingServices.Interfaces;
using MultiAgentCopilot.Common.Models.Chat;
using MultiAgentCopilot.Common.Models.Debug;

namespace MultiAgentCopilot.ChatInfrastructure.Interfaces
{
    public interface ISemanticKernelService
    {
        Task<Tuple<List<Message>, List<DebugLog>>> GetResponse(Message userMessage, List<Message> messageHistory, IBankDataService bankService, string tenantId, string userId);

        Task<string> Summarize(string sessionId, string userPrompt);

        Task<float[]> GenerateEmbedding(string text);
    }
}
```

</details>

<details>
  <summary>Completed code for <strong>ChatInfrastructure/Services/SemanticKernelService.cs</strong></summary>

<br>

```csharp
using System;
using System.Runtime;
using System.Data;
using Newtonsoft.Json;
using Azure.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Embeddings;

using MultiAgentCopilot.Common.Models.Chat;
using MultiAgentCopilot.ChatInfrastructure.Interfaces;
using MultiAgentCopilot.Common.Models.Configuration;
using MultiAgentCopilot.Common.Models.Debug;
using Message = MultiAgentCopilot.Common.Models.Chat.Message;
using MultiAgentCopilot.ChatInfrastructure.Factories;
using MultiAgentCopilot.ChatInfrastructure.Models;
using BankingServices.Interfaces;

namespace MultiAgentCopilot.ChatInfrastructure.Services;

public class SemanticKernelService : ISemanticKernelService, IDisposable
{
    readonly SemanticKernelServiceSettings _settings;
    readonly ILoggerFactory _loggerFactory;
    readonly ILogger<SemanticKernelService> _logger;
    readonly Kernel _semanticKernel;

    bool _serviceInitialized = false;
    string _prompt = string.Empty;
    string _contextSelectorPrompt = string.Empty;

    List<LogProperty> _promptDebugProperties;

    public bool IsInitialized => _serviceInitialized;

    public SemanticKernelService(
        IOptions<SemanticKernelServiceSettings> options,
        ILoggerFactory loggerFactory)
    {
        _settings = options.Value;
        _loggerFactory = loggerFactory;
        _logger = _loggerFactory.CreateLogger<SemanticKernelService>();
        _promptDebugProperties = new List<LogProperty>();

        _logger.LogInformation("Initializing the Semantic Kernel service...");

        var builder = Kernel.CreateBuilder();

        builder.Services.AddSingleton<ILoggerFactory>(loggerFactory);

        DefaultAzureCredential credential;
        if (string.IsNullOrEmpty(_settings.AzureOpenAISettings.UserAssignedIdentityClientID))
        {
            credential = new DefaultAzureCredential();
        }
        else
        {
            credential = new DefaultAzureCredential(new DefaultAzureCredentialOptions
            {
                ManagedIdentityClientId = _settings.AzureOpenAISettings.UserAssignedIdentityClientID
            });
        }
        builder.AddAzureOpenAIChatCompletion(
            _settings.AzureOpenAISettings.CompletionsDeployment,
            _settings.AzureOpenAISettings.Endpoint,
            credential);

        builder.AddAzureOpenAITextEmbeddingGeneration(
               _settings.AzureOpenAISettings.EmbeddingsDeployment,
               _settings.AzureOpenAISettings.Endpoint,
               credential);

        _semanticKernel = builder.Build();

        Task.Run(Initialize).ConfigureAwait(false);
    }

    public void Dispose()
    {
        // Dispose resources if any
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

    private void LogMessage(string key, string value)
    {
        _promptDebugProperties.Add(new LogProperty(key, value));
    }

    public async Task<Tuple<List<Message>, List<DebugLog>>> GetResponse(Message userMessage, List<Message> messageHistory, IBankDataService bankService, string tenantId, string userId)
    {
        try
        {
            ChatFactory agentChatGeneratorService = new ChatFactory();

            var agent = agentChatGeneratorService.BuildAgent(_semanticKernel, AgentType.CustomerSupport, _loggerFactory, bankService, tenantId, userId);

            ChatHistory chatHistory = [];

            // Load history
            foreach (var chatMessage in messageHistory)
            {
                if (chatMessage.SenderRole == "User")
                {
                    chatHistory.AddUserMessage(chatMessage.Text);
                }
                else
                {
                    chatHistory.AddAssistantMessage(chatMessage.Text);
                }
            }

            chatHistory.AddUserMessage(userMessage.Text);

            _promptDebugProperties = new List<LogProperty>();

            List<Message> completionMessages = new();
            List<DebugLog> completionMessagesLogs = new();

            await foreach (ChatMessageContent response in agent.InvokeAsync(chatHistory))
            {
                string messageId = Guid.NewGuid().ToString();
                completionMessages.Add(new Message(userMessage.TenantId, userMessage.UserId, userMessage.SessionId, response.AuthorName ?? string.Empty, response.Role.ToString(), response.Content ?? string.Empty, messageId));
            }
            return new Tuple<List<Message>, List<DebugLog>>(completionMessages, completionMessagesLogs);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error when getting response: {ErrorMessage}", ex.ToString());
            return new Tuple<List<Message>, List<DebugLog>>(new List<Message>(), new List<DebugLog>());
        }
    }

    public async Task<string> Summarize(string sessionId, string userPrompt)
    {
        try
        {
            // Use an AI function to summarize the text in 2 words
            var summarizeFunction = _semanticKernel.CreateFunctionFromPrompt(
                "Summarize the following text into exactly two words:\n\n{{$input}}",
                executionSettings: new OpenAIPromptExecutionSettings { MaxTokens = 10 }
            );

            // Invoke the function
            var summary = await _semanticKernel.InvokeAsync(summarizeFunction, new() { ["input"] = userPrompt });

            return summary.GetValue<string>() ?? "No summary generated";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error when getting response: {ErrorMessage}", ex.ToString());
            return string.Empty;
        }
    }

    public async Task<float[]> GenerateEmbedding(string text)
    {
        // Generate Embedding
        var embeddingModel = _semanticKernel.Services.GetRequiredService<ITextEmbeddingGenerationService>();

        var embedding = await embeddingModel.GenerateEmbeddingAsync(text);

        // Convert ReadOnlyMemory<float> to IList<float>
        return embedding.ToArray();
    }
}
```

</details>

## Next Steps

Proceed to [Multi-Agent Orchestration](./Module-04.md)

## Resources

- [Semantic Kernel Agent Framework](https://learn.microsoft.com/semantic-kernel/frameworks/agent)
- [Azure OpenAI Service documentation](https://learn.microsoft.com/azure/cognitive-services/openai/)
- [Azure Cosmos DB Vector Database](https://learn.microsoft.com/azure/cosmos-db/vector-database)
