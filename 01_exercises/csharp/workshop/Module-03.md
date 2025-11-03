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

In our multi agent application we have four agents: transactions agent, sales agent, customer support agent, and a coordinator agent to manage all of them. With the behavior of the agents defined in Prompty, we now need to implement the code that will allow the application to load the agent behavior for each of the agents.

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
1. Search for **//TO DO: Add In Process Tools** and paste the code **within** the method. Do **not** replace the entire method.

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
1. Search for **//TO DO: Add SetInProcessToolService** and paste code below .

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
1. Search for **//TO DO: Invoke SetInProcessToolService** and paste code below **within** the method. Do **not** replace the entire method.

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
1. Search for **//TO DO: Update GenerateEmbeddingAsync** and replace the code for **GenerateEmbeddingAsync()** method with the code 

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
1. Search for **//TO DO: Update SearchOfferTermsAsync** and replace the code for **SearchOfferTermsAsync()** method with the code below.

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


```
</details>
<details>
  <summary>Completed code for <strong>\Services\ChatService.cs</strong></summary>
<br>

```csharp


```
</details>
<details>
  <summary>Completed code for <strong>\Banking\Services\EmbeddingService.cs</strong></summary>
<br>

```csharp


```
</details>
<details>
  <summary>Completed code for <strong>\Banking\Services\BankingDataService.cs</strong></summary>
<br>

```csharp


```
</details>
<details>
  <summary>Completed code for <strong>\MultiAgentCopilot\Factories\AgentFactory.cs</strong></summary>
<br>

```csharp


```

</details>
<details>
  <summary>Completed code for <strong>\MultiAgentCopilot\Tools\TransactionTools.cs</strong></summary>
<br>

```csharp


```
</details>

## Next Steps

Proceed to Module 4 - [Multi-Agent Orchestration](./Module-04.md)
