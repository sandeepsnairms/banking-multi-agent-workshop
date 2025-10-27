# Module 03 - Agent Specialization

## Introduction

In this module, you'll learn how to implement agent specialization by creating specialized tools and functions that provide domain-specific functionality. You'll build individual agents that comprise a multi-agent system, each with their own expertise and capabilities.

## Learning Objectives

- Understand Microsoft Agent Framework tool and function architecture
- Implement semantic search and vector indexing integration with Azure Cosmos DB
- Learn how to define specialized agent roles and communication protocols
- Build domain-specific tools for banking operations and customer service

## Module Exercises

1. [Activity 1: Defining Bank Domain Data Models and Functions](#activity-1-defining-bank-domain-data-models-and-functions)  
1. [Activity 2: Defining Agent Behavior](#activity-2-defining-agent-behavior)  
1. [Activity 3: Integrating Bank Domain Functions as Tools](#activity-3-integrating-bank-domain-functions-as-tools)  
1. [Activity 4: Adding a Tool to the Agent](#activity-4-adding-a-tool-to-the-agent)  
1. [Activity 5: Building an Agent Dynamically](#activity-5-building-an-agent-dynamically)
1. [Activity 6: Semantic Search](#activity-6-semantic-search)
1. [Activity 7: Test your Work](#activity-7-test-your-work)

## Activity 1: Defining Bank Domain Data Models and Functions

When working with any kind of data we need to review our data models.

1. In VS Code, navigate to the **Banking** project.
1. Take a look at the names of the **Models** here. These are the domain-specific models we will utilize in our banking scenario.
1. Also review the **BankingDataService** class and understand the various database CRUD operations based on the models.

## Activity 2: Defining Agent Behavior

Agent behavior is defined using prompts. These can be as simple as text in a string variable. However, it is often better to store these as external text files. In this solution we will use a format called, *Prompty* to manage our prompts.

Prompty is an asset class and file format designed to streamline the development and management of prompts for Large Language Models (LLMs). By combining configuration settings, sample data, and prompt templates into a single .prompty file, Prompty enhances observability, understandability, and portability for developers, thereby accelerating the prompt engineering process.

### Understand Agent behavior using Prompty

In this activity we will review the existing Prompty files.

#### Common Agent Rules

1. In VS Code, navigate to the **MultiAgentCopilot** project.
1. In VS Code, navigate to the **/Prompts** folder.
1. Review the contents of **CommonAgentRules.prompty**.

The contents of this file don't define a single agent's behavior but provides a baseline for how all agents will behave. Think of it like a set of global rules for agents. All agents import the text from this prompt to govern their responses.

#### Coordinator Agent

1. Review the contents of **Coordinator.prompty**.

This agent is the coordinator for the entire multi-agent system we are building. Its purpose is to own the experience for users. It starts by greeting new users when they initiate a new session, then routes user requests to the correct agent(s) to handle on their behalf. Finally it asks for feedback on how it did its job.

#### Customer Support Agent

1. Review the contents of **CustomerSupport.prompty**.

This agent handles anything that appears to be a customer support request by a user. It can create, find and update services requests for users. It can also take certain action on behalf of users too.

#### Sales Agent

1. Review the contents of **Sales.prompty**.

This agent is used when customers ask questions about what kinds of services a bank offers. The data on the products the bank has are stored in Cosmos DB. This agent performs a vector search in Cosmos DB to find the most suitable products for a customer's request.

#### Transaction Agent

1. Review the contents of **Transactions.prompty**.

This agent handles any account-based transactions on behalf of the user including getting account balances, generating statements and doing fund transfers between accounts.

### Retrieving the prompty text for Agents

In our banking solution we have four agents: transactions agent, sales agent, customer support agent, and a coordinator agent to manage all of them. With the behavior of the agents defined in Prompty, we now need to implement the code that will allow the application to load the agent behavior for each of the agents.

1. In VS Code, navigate to the **/Models** folder.
1. Review the contents of **AgentTypes.cs**.

### Implementing the Agent Factory

We are now ready to complete the implementation for the **Agent Factory** created in the previous module. The **AgentFactory** will generate prompts based on the **agentType** parameter, allowing us to reuse the code and add more agents.

1. In VS Code, navigate to the **/Factories** folder.
1. Next, open the **AgentFactory.cs** class.

Next we need to replace our original hard-coded implementation from Module 2 to use the AgentType enum for our banking agents. It is also worth noting that it is here where the contents of the **CommonAgentRules.prompty** are included as part of the system prompts that define our agents.

1. Search for **TO DO: Add Agent Details** and paste the below code for **GetAgentName()**, **GetAgentDescription()** and **GetAgentPrompt()** :

**Note:** You will notice build errors for some of the updates you make during the activities in this module. These will be fixed in subsequent Activities.

These methods provide agent metadata and behavior definitions based on agent type. They load prompts from .prompty files and combine them with common agent rules.

```csharp
        /// <summary>
        /// Get agent prompt based on type
        /// </summary>
        private static string GetAgentPrompt(AgentType agentType)
        {
            string promptFile = $"{GetAgentName(agentType)}.prompty";

            string prompt = $"{File.ReadAllText($"Prompts/{promptFile}")}{File.ReadAllText("Prompts/CommonAgentRules.prompty")}";

            return prompt;
        }

        /// <summary>
        /// Get agent name based on type
        /// </summary>
        public static string GetAgentName(AgentType agentType)
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

```

## Activity 3: Integrating Bank Domain Functions as Tools

All banking domain code is encapsulated in a separate **BankingDataService** class. Let's add the banking domain functions to the agent Tools. For simplicity in this workshop, all functions reference BankingServices. However, agent tools can be any managed code that enables the LLM to interact with the outside world. The Base Tool, inherited by all Tools, contains common code for all Tools. For best results, the **Tool Functions** available to the agent should be consistent with the agent system prompts.

To save time, the code for SalesTools, CustomerSupportTools, and CoordinatorTools are already implemented. The code for TransactionTools is left for you to implement.

1. In VS Code, navigate to the **/Tools** folder.
1. Open the **TransactionTools.cs** file.
1. Paste the following code into the class below the constructor.

This tool function enables agents to add new account transactions to the banking system. It validates input parameters and delegates to the banking data service for persistence.

```csharp
        [Description("Adds a new Account Transaction request")]
        public async Task<ServiceRequest> AddFunTransferRequest(string tenantId, string userId,
            string debitAccountId,
            decimal amount,
            string requestAnnotation,
            string? recipientPhoneNumber = null,
            string? recipientEmailId = null)
        {
            _logger.LogTrace("Adding AccountTransaction request for User ID: {UserId}, Debit Account: {DebitAccountId}", userId, debitAccountId);

            // Ensure non-null values for recipientEmailId and recipientPhoneNumber
            string emailId = recipientEmailId ?? string.Empty;
            string phoneNumber = recipientPhoneNumber ?? string.Empty;

            return await _bankService.CreateFundTransferRequestAsync(tenantId, debitAccountId, userId, requestAnnotation, emailId, phoneNumber, amount);
        }

        [Description("Get the transactions history between 2 dates")]
        public async Task<List<BankTransaction>> GetTransactionHistory(string tenantId, string userId,string accountId, DateTime startDate, DateTime endDate)
        {
            _logger.LogTrace("Fetching AccountTransaction history for Account: {AccountId}, From: {StartDate} To: {EndDate}", accountId, startDate, endDate);
            return await _bankService.GetTransactionsAsync(tenantId, accountId, startDate, endDate);
        }
```

## Activity 4: Adding a Tool to the Agent
1. In VS Code, navigate to the **/Factories** folder
1. Open the **AgentFactory.cs** class.
1. Search for **//TO DO: Create Agent Tools** and paste code below .

```csharp
        /// <summary>
        /// Get tools for specific agent type using existing tool classes
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

```
### Adding InProcess tools in Agent Framework Service
1. In VS Code, navigate to the **/Services** folder
1. Open the **AgentFrameworkService.cs** class.
1. Search for **TO DO: Add In Process Tools** and paste the code below .

```csharp
if (_bankService != null)
{
    _agents = AgentFactory.CreateAllAgentsWithInProcessTools(_chatClient, _bankService, _loggerFactory);
}
``` 

## Activity 5: Building an Agent Dynamically

Similar to generating system prompts based on agent type, we need the Tools to be created dynamically. Next, we will implement a **CreateAllAgentsWithInProcessTools()** function that dynamically generates a Tool based on the agent type.

1. In VS Code, navigate to the **/Factories** folder
1. Open the **AgentFactory.cs** class.
1. Search for **//TO DO: Add Agent Creation with Tools** and paste code below .

```csharp
        /// <summary>
        /// Create all banking agents with proper instructions and tools
        /// </summary>
        public static List<AIAgent> CreateAllAgentsWithInProcessTools(IChatClient chatClient, BankingDataService bankService, ILoggerFactory loggerFactory)
        {           

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
                        description: GetAgentDescription(agentType)
                    );

                agents.Add(agent);
                logger.LogInformation("Created agent {AgentName} with {ToolCount} InProcess", agent.Name, aiFunctions.Count());
            }

            logger.LogInformation("Successfully created {AgentCount} banking agents", agents.Count);
            return agents;
        }
```

### Configure AgentFrameworkService to use In-Process tools

1. In VS Code, navigate to the **/Services** folder
1. Open the **AgentFrameworkService.cs** class.
1. Search for **TO DO: Add SetInProcessToolService** and paste code below .

```csharp
    /// <summary>
    /// Sets the in-process tool service for banking operations.
    /// </summary>
    /// <param name="bankService">The banking data service instance.</param>
    /// <returns>True if the service was set successfully.</returns>
    
    public bool SetInProcessToolService(BankingDataService bankService)
    {
        _bankService = bankService ?? throw new ArgumentNullException(nameof(bankService));
        _logger.LogInformation("InProcessToolService has been set.");
        return true;
    }
```

1. Navigate to **ChatService.cs** class.
1. Search for **// TO DO: Invoke SetInProcessToolService** and paste code below .

```csharp
            var embeddingClient = _afService.GetAzureOpenAIClient();
            var embeddingDeployment = _afService.GetEmbeddingDeploymentName();
            EmbeddingService embeddingService = new EmbeddingService(embeddingClient, embeddingDeployment);
            _bankService = new BankingDataService(embeddingService, cosmosDBService.Database, cosmosDBService.AccountDataContainer, cosmosDBService.UserDataContainer, cosmosDBService.AccountDataContainer, cosmosDBService.OfferDataContainer, loggerFactory);

            _afService.SetInProcessToolService(_bankService);

```

Now that we can build Agents, we can make the agent build process dynamic based on the **agentType** parameter. Next, we will modify the **BuildAgent()** function within the **AgentFactory** class to dynamically add Tools to the agents.

1. In VS Code, navigate to the **/Services** folder
1. Open the **AgentFrameworkService.cs** class.
1. Update the **GetResponse()** method with the code below .

```csharp

    public async Task<Tuple<List<Message>, List<DebugLog>>> GetResponse(
        Message userMessage,
        List<Message> messageHistory,
        BankingDataService bankService,
        string tenantId,
        string userId)
    {
        try
        {

            messageHistory.Add(userMessage);
            var chatHistory = ConvertToAIChatMessages(messageHistory);
            chatHistory.Add(new ChatMessage(ChatRole.User, userMessage.Text));
            var agentName="Coordinator";
            var bankAgent = _agents.FirstOrDefault(s => s.Name == agentName);
            var responseText = bankAgent.RunAsync(chatHistory).GetAwaiter().GetResult().Text;
            return CreateResponseTuple(userMessage, responseText, agentName);
            
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error when getting response: {ErrorMessage}", ex.Message);
            return new Tuple<List<Message>, List<DebugLog>>(new List<Message>(), new List<DebugLog>());
        }
    }
```

## Activity 6: Semantic Search

The Sales Agent in this banking application performs a vector search in Cosmos DB to search for banking products and services for users. In this activity, you will learn how to configure vector indexing and search in Azure Cosmos DB and explore the container and vector indexing policies. Then learn how to implement vector search using for Semantic Kernel.

### Create Data Model for Vector Search

Data Models used for Vector Search in Semantic Kernel need to be enhanced with additional attributes. We will use **OfferTerm** as vector search enabled data model.

1. In VS Code, navigate to the **Banking** project.
1. Navigate to the **/Models** folder.
1. Review the **OfferTerm.cs** class. Notice Vector attribute is of type `ReadOnlyMemory<float>`


### Initialize the Embedding client to vectorize terms

1. Remain in the **Banking** project.
1. Open the the **Services/EmbeddingService.cs** file.
1. Search for **// TO DO: Update GenerateEmbeddingAsync** and replace the code for **GenerateEmbeddingAsync()**method with the code 

```csharp
        public async Task<float[]> GenerateEmbeddingAsync(string text)
        {

            var embeddingClient = _client.GetEmbeddingClient(_deployment);
            var result = await embeddingClient.GenerateEmbeddingsAsync(new List<string> { text });
            var vector = result.Value[0].ToFloats().ToArray();

            return vector;

        }
```
### Update BankingDataService to include vector search
1. Remain in the **Banking** project.
1. Open the **Services/BankingDataService.cs** file.
1. Search for **//TO DO : Update SearchOfferTermsAsync** and replace the code for **SearchOfferTermsAsync()**method with the code below.

```csharp
        public async Task<List<OfferTerm>> SearchOfferTermsAsync(string tenantId, AccountType accountType, string requirementDescription)
        {
            try
            {

                // Generate embeddings for the requirement description
                var queryVector = await _embeddingService.GenerateEmbeddingAsync(requirementDescription);


                // Build the vector search query with filters
                var query = new QueryDefinition(@"
                            SELECT 
                                c.id, 
                                c.tenantId, 
                                c.offerId, 
                                c.name, 
                                c.text, 
                                c.type, 
                                c.accountType, 
                                VectorDistance(c.vector, @queryVector) AS similarityScore
                            FROM c 
                            WHERE c.type = @type 
                              AND c.tenantId = @tenantId 
                              AND c.accountType = @accountType
                            ORDER BY VectorDistance(c.vector, @queryVector)
                        ")
                .WithParameter("@queryVector", queryVector)
                .WithParameter("@type", "Term")
                .WithParameter("@tenantId", tenantId)
                .WithParameter("@accountType", accountType.ToString());

                // Define partition key
                var partitionKey = new PartitionKey(tenantId);

                // Run query
                var offerTerms = new List<OfferTerm>();
                using (FeedIterator<OfferTerm> feedIterator = _offerData.GetItemQueryIterator<OfferTerm>(
                    query,
                    requestOptions: new QueryRequestOptions
                    {
                        PartitionKey = partitionKey,
                        MaxItemCount = 10 // Limit for performance
                    }))
                {
                    while (feedIterator.HasMoreResults)
                    {
                        FeedResponse<OfferTerm> response = await feedIterator.ReadNextAsync();
                        offerTerms.AddRange(response);
                    }
                }

                return offerTerms;

            }
            catch (Exception ex)
            {
                _logger.LogError("Error searching offer terms: {Message}", ex.Message);
                return new List<OfferTerm>();
            }
        }
```


## Activity 7: Test your Work

With the activities in this module complete, it is time to test your work.

### Start the Backend

1. Return to the open terminal for the backend app in VS Code. Ensure you are in `01_exercises\csharp\src\MultiAgentCopilot`. Type `dotnet run`

### Start a Chat Session

1. Return to the frontend application in your browser.
1. Send the below message to test the current *Coordinator* AgentType.
```text
I'm looking for a high interest savings account
```
1. View the response in the frontend.
1. Return to VS Code, Notice the output in the terminal showing the action taken by the Coordinator Agent to your prompt.
1. Within the backend terminal, press **Ctrl + C** to stop the backend application.
1. Locate this line of code, *var agentName="Coordinator";* in **\Services\AgentFrameworkService.cs**
1. Replace it with the line of code below.

```csharp
var agentName="Sales";
```

1. Return to the open terminal for the backend app in VS Code and type `dotnet run`
1. Return to the frontend application in your browser.
1. Send the same message to test the current *Sales* AgentType.
```text
I'm looking for a high interest savings account
```
1. View the response in the frontend.

 
### Stop the Application

1. Within the backend terminal, press **Ctrl + C** to stop the backend application.

## Validation Checklist

- [ ] Each Agent response is per the corresponding prompty file contents and the Tool functions.
- [ ] Semantic Search functions correctly

## Module Solution

The following sections include the completed code for this Module. Copy and paste these into your project if you run into issues and cannot resolve.

<details>
  <summary>Completed code for <strong>\Services\AgentFrameworkService.cs</strong></summary>
<br>

```csharp
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using MultiAgentCopilot.Helper;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Embeddings;
using Microsoft.Extensions.AI;

using Azure.Identity;
using MultiAgentCopilot.Factories;
using MultiAgentCopilot.Models.Debug;
using MultiAgentCopilot.Models.Chat;
using MultiAgentCopilot.Models.Configuration;

using System.Text;
using MultiAgentCopilot.Models;
using Microsoft.SemanticKernel.Agents;
using AgentFactory = MultiAgentCopilot.Factories.AgentFactory;
namespace MultiAgentCopilot.Services;

public class SemanticKernelService :  IDisposable
{
    readonly SemanticKernelServiceSettings _skSettings;
    readonly ILoggerFactory _loggerFactory;
    readonly ILogger<SemanticKernelService> _logger;
    readonly Kernel _semanticKernel;


    bool _serviceInitialized = false;
    string _prompt = string.Empty;
    string _contextSelectorPrompt = string.Empty;

    List<LogProperty> _promptDebugProperties;

    public bool IsInitialized => _serviceInitialized;

    public SemanticKernelService(
        IOptions<SemanticKernelServiceSettings> skOptions,
        ILoggerFactory loggerFactory)
    {
        _skSettings = skOptions.Value;
        _loggerFactory = loggerFactory;
        _logger = _loggerFactory.CreateLogger<SemanticKernelService>();
        _promptDebugProperties = new List<LogProperty>();

        _logger.LogInformation("Initializing the Semantic Kernel service...");

        var builder = Kernel.CreateBuilder();

        //TO DO: Update SemanticKernelService constructor
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

        _semanticKernel = builder.Build();


        Task.Run(Initialize).ConfigureAwait(false);
    }

    //TO DO: Add GetResponse function

    public async Task<Tuple<List<Message>, List<DebugLog>>> GetResponse(Message userMessage, List<Message> messageHistory, BankingDataService bankService, string tenantId, string userId)
    {
        try
        {
            AgentFactory agentFactory = new AgentFactory();

            var agent = agentFactory.BuildAgent(_semanticKernel, AgentType.CustomerSupport, _loggerFactory, bankService, tenantId, userId);

            ChatHistory chatHistory = new();

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

            // Create an AgentThread using the ChatHistory object
            AgentThread agentThread = new ChatHistoryAgentThread(chatHistory);

            _promptDebugProperties = new List<LogProperty>();

            List<Message> completionMessages = new();
            List<DebugLog> completionMessagesLogs = new();


            await foreach (ChatMessageContent response in agent.InvokeAsync(userMessage.Text, agentThread))
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

    //TO DO: Add Summarize function
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


using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.Agents.Chat;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.SemanticKernel.Connectors.AzureOpenAI;

using OpenAI.Chat;
using System.Text.Json;
using MultiAgentCopilot.StructuredFormats;
using MultiAgentCopilot.Models.ChatInfoFormats;
using MultiAgentCopilot.Logs;
using MultiAgentCopilot.Models;
using MultiAgentCopilot.Services;
using static MultiAgentCopilot.StructuredFormats.ChatResponseFormatBuilder;
using MultiAgentCopilot.Tools;


namespace MultiAgentCopilot.Factories
{
    internal class AgentFactory
    {
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
                    var salesTool = new SalesTool(loggerFactory.CreateLogger<SalesTool>(), bankService, tenantId, userId);
                    agentKernel.Tools.AddFromObject(salesTool);
                    break;
                case AgentType.Transactions:
                    var transactionsTool = new TransactionTool(loggerFactory.CreateLogger<TransactionTool>(), bankService, tenantId, userId);
                    agentKernel.Tools.AddFromObject(transactionsTool);
                    break;
                case AgentType.CustomerSupport:
                    var customerSupportTool = new CustomerSupportTool(loggerFactory.CreateLogger<CustomerSupportTool>(), bankService, tenantId, userId);
                    agentKernel.Tools.AddFromObject(customerSupportTool);
                    break;
                case AgentType.Coordinator:
                    var CoordinatorTool = new CoordinatorTool(loggerFactory.CreateLogger<CoordinatorTool>(), bankService, tenantId, userId);
                    agentKernel.Tools.AddFromObject(CoordinatorTool);
                    break;
                default:
                    throw new ArgumentException("Invalid Tool name");
            }

            return agentKernel;
        }
    }
}        
```

</details>

<details>
  <summary>Completed code for <strong>\AgentTools\TransactionTool.cs</strong></summary>
<br>

```csharp
using Microsoft.SemanticKernel;
using System.ComponentModel;
using MultiAgentCopilot.Models.Banking;
using MultiAgentCopilot.Services;

namespace MultiAgentCopilot.Tools
{
    public class TransactionTool : BaseTool
    {
        public TransactionTool(ILogger<BaseTool> logger, BankingDataService bankService, string tenantId, string userId)
         : base(logger, bankService, tenantId, userId)
        {
        }

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
    }
} 
    
```

</details>

<details>
  <summary>Completed code for <strong>\AgentTools\SalesTool.cs</strong></summary>
<br>

```csharp
using Microsoft.SemanticKernel;
using System.ComponentModel;
using  MultiAgentCopilot.Models.Banking;
using MultiAgentCopilot.Services;

namespace MultiAgentCopilot.Tools
{
    internal class SalesTool : BaseTool
    {
        public SalesTool(ILogger<BaseTool> logger, BankingDataService bankService, string tenantId, string userId )
            : base(logger, bankService, tenantId, userId)
        {
        }
               

        [KernelFunction]
        [Description("Register a new account.")]
        public async Task<ServiceRequest> RegisterAccount(string userId, AccountType accType, Dictionary<string,string> fulfilmentDetails)
        {
            _logger.LogTrace($"Registering Account. User ID: {userId}, Account Type: {accType}");
            return await _bankService.CreateFulfilmentRequestAsync(_tenantId, string.Empty,_userId,string.Empty,fulfilmentDetails);
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
  <summary>Completed code for <strong>\Models\Banking\OfferTerm.cs</strong></summary>
<br>

```csharp
using Microsoft.Extensions.VectorData;

namespace MultiAgentCopilot.Models.Banking
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
  <summary>Completed code for <strong>\Services\BankingDataService.cs</strong></summary>
<br>

```csharp
using System.Diagnostics;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Fluent;
using Container = Microsoft.Azure.Cosmos.Container;
using Azure.Identity;
using System.Text;
using MultiAgentCopilot.Helper;
using MultiAgentCopilot.Models.Configuration;
using MultiAgentCopilot.Models.Banking;

using PartitionKey = Microsoft.Azure.Cosmos.PartitionKey;

using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.AzureCosmosDBNoSQL;

using System.Text.Json;

using Microsoft.SemanticKernel.Connectors.AzureOpenAI;
using Microsoft.Extensions.VectorData;
using System.Linq.Expressions; 


namespace  MultiAgentCopilot.Services
{
    public class BankingDataService
    {
        private readonly Container _accountData;
        private readonly Container _userData;
        private readonly Container _requestData;
        private readonly Container _offerData;

        private readonly AzureCosmosDBNoSQLVectorStoreRecordCollection<OfferTerm> _offerDataVectorStore;
        private readonly AzureOpenAITextEmbeddingGenerationService _textEmbeddingGenerationService;

        private readonly Database _database;

        private readonly ILogger _logger;

        public bool IsInitialized { get; private set; }

        readonly Kernel _semanticKernel;




        public BankingDataService(
           Database database, Container accountData, Container userData, Container requestData, Container offerData,
           SemanticKernelServiceSettings skSettings,
           ILoggerFactory loggerFactory)
        {

            _database = database;
            _accountData = accountData;
            _userData = userData;
            _requestData = requestData;
            _offerData = offerData;

            _logger = loggerFactory.CreateLogger<BankingDataService>();

            _logger.LogInformation("Initializing Banking service.");


            //To DO: Add vector search initialization code here

            DefaultAzureCredential credential;
            if (string.IsNullOrEmpty(skSettings.AzureOpenAISettings.UserAssignedIdentityClientID))
            {
                credential = new DefaultAzureCredential();
            }
            else
            {
                credential = new DefaultAzureCredential(new DefaultAzureCredentialOptions
                {
                    ManagedIdentityClientId = skSettings.AzureOpenAISettings.UserAssignedIdentityClientID
                });

            }

            _textEmbeddingGenerationService = new(
                    deploymentName: skSettings.AzureOpenAISettings.EmbeddingsDeployment, // Name of deployment, e.g. "text-embedding-ada-002".
                    endpoint: skSettings.AzureOpenAISettings.Endpoint,           // Name of Azure OpenAI service endpoint, e.g. https://myaiservice.openai.azure.com.
                    credential: credential);

            var jsonSerializerOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
            var vectorStoreOptions = new AzureCosmosDBNoSQLVectorStoreRecordCollectionOptions<OfferTerm> { PartitionKeyPropertyName = "TenantId", JsonSerializerOptions = jsonSerializerOptions };
            _offerDataVectorStore = new AzureCosmosDBNoSQLVectorStoreRecordCollection<OfferTerm>(_database, _offerData.Id, vectorStoreOptions);

            _logger.LogInformation("Banking service initialized.");
        }

        public async Task<BankUser> GetUserAsync(string tenantId,string userId)
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

        public async Task<ServiceRequest> CreateComplaintAsync(string tenantId, string accountId, string userId, string requestAnnotation)
        {
            var req = new ServiceRequest(ServiceRequestType.Complaint, tenantId, accountId, userId, requestAnnotation, string.Empty, string.Empty, 0,  DateTime.MinValue, null);
            return await AddServiceRequestAsync(req);
        }

        public async Task<ServiceRequest> CreateFulfilmentRequestAsync(string tenantId, string accountId, string userId, string requestAnnotation, Dictionary<string,string> fulfilmentDetails)
        {
            var req = new ServiceRequest(ServiceRequestType.Fulfilment, tenantId, accountId, userId, requestAnnotation, string.Empty, string.Empty, 0,  DateTime.MinValue, fulfilmentDetails);
            return await AddServiceRequestAsync(req);
        }


        private async Task<ServiceRequest> AddServiceRequestAsync(ServiceRequest req)
        {
            try
            {
                var partitionKey = PartitionManager.GetAccountsDataFullPK(req.TenantId,req.AccountId);
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

        public async Task<List<OfferTerm>> SearchOfferTermsAsync(string tenantId, AccountType accountType, string requirementDescription)
        {
            try
            {
                // Generate Embedding
                ReadOnlyMemory<float> embedding = (await _textEmbeddingGenerationService.GenerateEmbeddingsAsync(
                       new[] { requirementDescription }
                   )).FirstOrDefault();


                string accountTypeString = accountType.ToString();

                // filters as LINQ expression
                Expression<Func<OfferTerm, bool>> linqFilter = term =>
                    term.TenantId == tenantId &&
                    term.Type == "Term" &&
                    term.AccountType == "Savings";

                var options = new VectorSearchOptions<OfferTerm>
                {
                    VectorProperty = term => term.Vector, // Correctly specify the vector property as a lambda expression
                    Filter = linqFilter, // Use the LINQ expression here
                    Top = 10,
                    IncludeVectors = false
                };


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

## Next Steps

Proceed to Module 4 - [Multi-Agent Orchestration](./Module-04.md)
