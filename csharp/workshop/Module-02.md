# Module 02 - Connecting Agents to Memory

## Introduction

In this Module you'll connect your agent to Azure Cosmos DB to provide memory for chat history and state management for your agents to provide durability and context-awareness in your agent interactions.

## Learning Objectives and Activities

- Learn the basics for Azure Cosmos DB for storing state and chat history
- Learn how to integrate agent frameworks to Azure Cosmos DB
- Test connectivity to Azure Cosmos DB works

## Module Exercises

1. [Activity 1: Create a Simple Banking Agent](#activity-1-create-a-simple-banking-agent)
1. [Activity 2: Connecting Agent Frameworks to Azure Cosmos DB](#activity-2-connecting-agent-frameworks-to-azure-cosmos-db)
1. [Activity 3: Test your Work](#activity-3-test-your-work)

## Activity 1: Session Memory Persistence in Agent Frameworks

In this session you will get an overview of memory and how it works for Semantic Kernel Agents and LangGraph and learn the basics for how to configure and connect both to Azure Cosmos DB as a memory store for both chat history and/or state management.

## Activity 1: Create a Simple Banking Agent

In this hands-on exercise, we will evolve the agent that translated responses into French into an agent that is intended for the banking scenario we are building here. We will also take the first step to learning about prompts here as well.

### Add Agent Factory

Agents are autonomous systems that use LLMs to process inputs, make decisions, and generate responses based on predefined goals. They can integrate with external tools, retrieve information, and adapt dynamically to different tasks. We are next going to implement a `AgentFactory` class that enables the creation of agents for various scenarios. It uses the GetAgentPrompts to define the agent prompts.

In your IDE, within the `/Factories` folder, open `AgentFactory.cs` and paste the code below:

```csharp
        private string GetAgentName()
        {

            return "FrontDeskAgent";
        }


        private string GetAgentPrompts()
        {

            string prompt = "You are a front desk agent in a bank. Respond to the user queries professionally. Provide professional and helpful responses to user queries.Use your knowledge of banking services and procedures to address user queries accurately.";
            return prompt;
        }


        public ChatCompletionAgent BuildAgent(Kernel kernel, ILoggerFactory loggerFactory, BankingDataService bankService, string tenantId, string userId)
        {
            ChatCompletionAgent agent = new ChatCompletionAgent
            {
                Name = GetAgentName(),
                Instructions = $"""{GetAgentPrompts()}""",
                Kernel = kernel.Clone()
            };

            return agent;
        }
```

We now have an agent that executes instructions provided as prompts.

Next lets add some memory so that the agent can remember the previous messages and doesn't loose context of the chat session.

## Activity 2: Connecting Agent Frameworks to Azure Cosmos DB

In this activity, you will learn how to initialize Azure Cosmos DB and integrate with an agent framework to provide persistent memory for chat history and state management.

In your IDE navigate to the `/Services` folder. Open `SemanticKernelService.cs`

### Update GetResponse() function in SemanticKernelService

The `GetResponse()` function is the main entry point for our multi-agent application. Within that function, a variable named, `messageHistory` stores a list of historical messages from the chat session. The `chatHistory` object is used to construct this history and passed to the Semantic Kernel Chat Completion Agent object. The `completionMessages` list is used to store the response received from the agent which then needs to be persisted in Cosmos DB for the next iteration of the agent.

We're going to modify this function to provide that persistence with Cosmos DB.

In your IDE, open the `SemanticKernelService.cs` and navigate to the `GetResponse()` function.

Replace all of the code within the `Try` block with the code below:

```csharp
            AgentFactory agentFactory = new AgentFactory();

            var agent = agentFactory.BuildAgent(_semanticKernel, _loggerFactory, bankService, tenantId, userId);

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

```

#### Update Chat Service

We can now update our Chat Service to store the messages generated between users and agents. In this step, we will add a new function that first calls the Cosmos DB service to get a Session object from our database. The Session object is part of an object hierarchy that defines the conversations between users and agents. A session has a name and also an array of messages for that conversation topic. You can view this hierarchy by navigating to its definition stored in `/Models/Chat/Session.cs`

With a reference to the current session returned from the CosmosDBService, this function then calls our newly implemented function to update the messages within the session object with any new or updated messages. Typically, this would include a single user prompt, followed by one or more responses from the agents.

To begin, navigate to the `/Services` folder and open the `ChatService.cs` class.

Paste the the below code within the class

```csharp
/// <summary>
/// Add user prompt and AI assistance response to the chat session message list object and insert into the data service as a transaction.
/// </summary>
private async Task AddPromptCompletionMessagesAsync(string tenantId, string userId, string sessionId, Message promptMessage, List<Message> completionMessages, List<DebugLog> completionMessageLogs)
{
    var session = await _cosmosDBService.GetSessionAsync(tenantId, userId, sessionId);

    completionMessages.Insert(0, promptMessage);
    await _cosmosDBService.UpsertSessionBatchAsync(completionMessages, completionMessageLogs, session);
}
```

#### Utilize and Store History in GetChatCompletionAsync

Locate `GetChatCompletionAsync()`, then update the function with the code in the `Try` block below:

```csharp
            ArgumentNullException.ThrowIfNull(sessionId);

            // Retrieve conversation, including latest prompt.
            var archivedMessages = await _cosmosDBService.GetSessionMessagesAsync(tenantId, userId, sessionId);

            // Add both prompt and completion to cache, then persist in Cosmos DB
            var userMessage = new Message(tenantId,userId,sessionId, "User","User", userPrompt);

            // Generate the completion to return to the user
            var result = await _skService.GetResponse(userMessage, archivedMessages,_bankService,tenantId,userId);

            await AddPromptCompletionMessagesAsync(tenantId, userId,sessionId, userMessage, result.Item1, result.Item2);

            return result.Item1;
```

## Activity 3: Test your Work

With the activities in this module complete, it is time to test your work.

### Start the Backend

- Return to the open terminal for the backend app in VS Code and type `dotnet run`

### Start the Frontend

- Return to the frontend terminal and type `ng serve`
- Navigate to, <http://localhost:4200> in your browser

### Start a Chat Session

We have a new agent now that thinks it works for a bank. So in our test here we are going to ask it banking-related questions.

1. Open the frontend app.
1. Start a new conversation.
1. Send the following message:

   ```text
   Can a senior citizen open a savings account?
   ```

1. Wait for the Agent response.
1. Send another message:

   ```text
   Does the interest rate vary?
   ```

1. Expected response: The Agent's response is contextually correct for the  whole chat session.

1. You should see something like the output below.

    ![Test output Module 2](./media/module-02/test-output.png)

### Stop the Application

- Return to VS Code.
- In the frontend terminal, press **Ctrl + C** to stop the frontend application.
- Select the backend terminal, press **Ctrl + C** to stop the backend application.

## Validation Checklist

Your implementation is successful if:

- [ ] Your app compiles with no errors.
- [ ] Your agent successfully responds with contextually correct information.

## Module Solution

The following sections include the completed code for this Module. Copy and paste these into your project if you run into issues and cannot resolve.

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
        public static string GetAgentName()
        {
            string name = "FrontDeskAgent";
            return name;
        }

        public static string GetAgentPrompts()
        {
            string prompt = "You are a front desk agent in a bank. Respond to the user queries professionally. Provide professional and helpful responses to user queries.Use your knowledge of banking services and procedures to address user queries accurately.";
            return prompt;
        }
    }
}
```

</details>

<details>
  <summary>Completed code for <strong>ChatInfrastructure/Factories/ChatFactory.cs</strong></summary>

<br>

```csharp
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

    public async Task<Tuple<List<Message>, List<DebugLog>>> GetResponse(Message userMessage, List<Message> messageHistory, string tenantId, string userId)
    {
        try
        {
            //Replace from here
            ChatFactory agentChatGeneratorService = new ChatFactory();

            var agent = agentChatGeneratorService.BuildAgent(_semanticKernel, _loggerFactory, tenantId, userId);

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
                completionMessages.Add(new Message(
                    userMessage.TenantId,
                    userMessage.UserId,
                    userMessage.SessionId,
                    response.AuthorName ?? string.Empty,
                    response.Role.ToString(),
                    response.Content ?? string.Empty,
                    messageId));
            }
            return new Tuple<List<Message>, List<DebugLog>>(completionMessages, completionMessagesLogs);
            //end replace
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

<details>
  <summary>Completed code for <strong>ChatInfrastructure/Interfaces/ICosmosDBService.cs</strong></summary>

<br>

```csharp
using MultiAgentCopilot.Common.Models.Chat;
using MultiAgentCopilot.Common.Models.Debug;
using Newtonsoft.Json.Linq;
using System.Text.Json;

namespace MultiAgentCopilot.ChatInfrastructure.Interfaces;

public interface ICosmosDBService
{

    /// <summary>
    /// Gets a list of all current chat sessions.
    /// </summary>
    /// <returns>List of distinct chat session items.</returns>
    Task<List<Session>> GetUserSessionsAsync(string tenantId, string userId);

    /// <summary>
    /// Gets a list of all current chat messages for a specified session identifier.
    /// </summary>
    /// <param name="sessionId">Chat session identifier used to filter messsages.</param>
    /// <returns>List of chat message items for the specified session.</returns>
    Task<List<Message>> GetSessionMessagesAsync(string tenantId, string userId, string sessionId);

    /// <summary>
    /// Performs a point read to retrieve a single chat session item.
    /// </summary>
    /// <returns>The chat session item.</returns>
    Task<Session> GetSessionAsync(string tenantId, string userId,string sessionId);

    /// <summary>
    /// Creates a new chat session.
    /// </summary>
    /// <param name="session">Chat session item to create.</param>
    /// <returns>Newly created chat session item.</returns>
    Task<Session> InsertSessionAsync(Session session);

    /// <summary>
    /// Creates a new chat message.
    /// </summary>
    /// <param name="message">Chat message item to create.</param>
    /// <returns>Newly created chat message item.</returns>
    Task<Message> InsertMessageAsync(Message message);

    /// <summary>
    /// Updates an existing chat message.
    /// </summary>
    /// <param name="message">Chat message item to update.</param>
    /// <returns>Revised chat message item.</returns>
    Task<Message> UpdateMessageAsync(Message message);

    /// <summary>
    /// Updates a message's rating through a patch operation.
    /// </summary>
    /// <param name="id">The message id.</param>
    /// <param name="sessionId">The message's partition key (session id).</param>
    /// <param name="rating">The rating to replace.</param>
    /// <returns>Revised chat message item.</returns>
    Task<Message> UpdateMessageRatingAsync(string tenantId, string userId, string sessionId, string messageId, bool? rating);

    /// <summary>
    /// Updates an existing chat session.
    /// </summary>
    /// <param name="session">Chat session item to update.</param>
    /// <returns>Revised created chat session item.</returns>
    Task<Session> UpdateSessionAsync(Session session);

    /// <summary>
    /// Updates a session's name through a patch operation.
    /// </summary>
    /// <param name="id">The session id.</param>
    /// <param name="name">The session's new name.</param>
    /// <returns>Revised chat session item.</returns>
    Task<Session> UpdateSessionNameAsync(string tenantId, string userId,string id, string name);

    /// <summary>
    /// Batch deletes an existing chat session and all related messages.
    /// </summary>
    /// <param name="sessionId">Chat session identifier used to flag messages and sessions for deletion.</param>
    Task DeleteSessionAndMessagesAsync(string tenantId, string userId,string sessionId);

   
    Task<DebugLog> GetChatCompletionDebugLogAsync(string tenantId, string userId,string sessionId, string debugLogId);

    Task<bool> InsertDocumentAsync(string containerName, JObject document);

    /// <summary>
    /// Batch create or update chat messages and session.
    /// </summary>
    /// <param name="messages">Chat message and session items to create or replace.</param>
    Task UpsertSessionBatchAsync(List<Message> messages, List<DebugLog> debugLogs, Session session);

}
```

</details>

<details>
  <summary>Completed code for <strong>ChatInfrastructure/Services/CosmosDBService.cs</strong></summary>

<br>

```csharp
using Azure.Identity;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Fluent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MultiAgentCopilot.ChatInfrastructure.Interfaces;
using MultiAgentCopilot.Common.Helper;
using MultiAgentCopilot.Common.Models.Chat;
using MultiAgentCopilot.Common.Models.Configuration;
using MultiAgentCopilot.Common.Models.Debug;
using Newtonsoft.Json.Linq;
using System.Diagnostics;
using Container = Microsoft.Azure.Cosmos.Container;
using Message = MultiAgentCopilot.Common.Models.Chat.Message;
using PartitionKey = Microsoft.Azure.Cosmos.PartitionKey;

namespace MultiAgentCopilot.ChatInfrastructure.Services
{
    /// <summary>
    /// Service to access Azure Cosmos DB for NoSQL.
    /// </summary>
    public class CosmosDBService : ICosmosDBService
    {
        private readonly Container _chatData;
        private readonly Container _userData;
        private readonly Container _offersData;
        private readonly Container _accountsData;

        private readonly Database _database;
        private readonly CosmosDBSettings _settings;
        private readonly ILogger _logger;


        public CosmosDBService(
            IOptions<CosmosDBSettings> settings,
            ILogger<CosmosDBService> logger)
        {
            _settings = settings.Value;
            ArgumentException.ThrowIfNullOrEmpty(_settings.CosmosUri);

            _logger = logger;
            _logger.LogInformation("Initializing Cosmos DB service.");

            if (!_settings.EnableTracing)
            {
                Type defaultTrace = Type.GetType("Microsoft.Azure.Cosmos.Core.Trace.DefaultTrace,Microsoft.Azure.Cosmos.Direct");
                TraceSource traceSource = (TraceSource)defaultTrace.GetProperty("TraceSource").GetValue(null);
                traceSource.Switch.Level = SourceLevels.All;
                traceSource.Listeners.Clear();
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
            CosmosClient client = new CosmosClientBuilder(_settings.CosmosUri, credential)
                .WithSerializerOptions(options)
                .WithConnectionModeGateway()
                .Build();

            _database = client?.GetDatabase(_settings.Database) ??
                        throw new ArgumentException("Unable to connect to existing Azure Cosmos DB database.");

            _chatData = _database.GetContainer(_settings.ChatDataContainer.Trim());
            _userData = _database.GetContainer(_settings.UserDataContainer.Trim());
            _offersData = _database.GetContainer(_settings.OfferDataContainer.Trim());
            _accountsData = _database.GetContainer(_settings.AccountsContainer.Trim());

            _logger.LogInformation("Cosmos DB service initialized.");
        }

        public async Task<List<Session>> GetUserSessionsAsync(string tenantId, string userId)
        {
            try
            {
                QueryDefinition query = new QueryDefinition("SELECT DISTINCT * FROM c WHERE c.type = @type")
                    .WithParameter("@type", nameof(Session));

                var partitionKey= PartitionManager.GetChatDataPartialPK(tenantId, userId);
                FeedIterator<Session> response = _chatData.GetItemQueryIterator<Session>(query, null, new QueryRequestOptions() { PartitionKey = partitionKey });

                List<Session> output = new();
                while (response.HasMoreResults)
                {
                    FeedResponse<Session> results = await response.ReadNextAsync();
                    output.AddRange(results);
                }

                return output;
            }
            catch (CosmosException ex)
            {
                _logger.LogError(ex.ToString());
                throw;
            }
        }

        public async Task<Session> GetSessionAsync(string tenantId, string userId, string sessionId)
        {
            try
            { 
                var partitionKey = PartitionManager.GetChatDataFullPK(tenantId, userId,sessionId);

                return await _chatData.ReadItemAsync<Session>(
                    id: sessionId,
                    partitionKey: partitionKey);
            }
            catch (CosmosException ex)
            {
                _logger.LogError(ex.ToString());
                throw;
            }
        }

        public async Task<List<Message>> GetSessionMessagesAsync(string tenantId, string userId,string sessionId)
        {
            try
            {
                QueryDefinition query =
                    new QueryDefinition("SELECT * FROM c WHERE c.type = @type")
                        .WithParameter("@type", nameof(Message));

                var partitionKey = PartitionManager.GetChatDataFullPK(tenantId, userId, sessionId);

                FeedIterator<Message> results = _chatData.GetItemQueryIterator<Message>(query, null, new QueryRequestOptions() { PartitionKey = partitionKey });

                List<Message> output = new();
                while (results.HasMoreResults)
                {
                    FeedResponse<Message> response = await results.ReadNextAsync();
                    output.AddRange(response);
                }

                return output;
            }
            catch (CosmosException ex)
            {
                _logger.LogError(ex.ToString());
                throw;
            }
        }

        public async Task<Session> InsertSessionAsync(Session session)
        {
            try
            { 
                var partitionKey = PartitionManager.GetChatDataFullPK(session.TenantId, session.UserId,session.SessionId);

                var response= await _chatData.CreateItemAsync(
                    item: session,
                    partitionKey: partitionKey
                );

                return response;
            }
            catch (CosmosException ex)
            {
                _logger.LogError(ex.ToString());
                throw;
            }
        }

        public async Task<Message> InsertMessageAsync(Message message)
        {
            try
            { 
                var partitionKey = PartitionManager.GetChatDataFullPK(message.TenantId, message.UserId, message.SessionId);

                return await _chatData.CreateItemAsync(
                    item: message,
                    partitionKey: partitionKey
                );
            }
            catch (CosmosException ex)
            {
                _logger.LogError(ex.ToString());
                throw;
            }
        }

        public async Task<Message> UpdateMessageAsync(Message message)
        {
            try
            { 
                var partitionKey = PartitionManager.GetChatDataFullPK(message.TenantId, message.UserId, message.SessionId);

                return await _chatData.ReplaceItemAsync(
                    item: message,
                    id: message.Id,
                    partitionKey: partitionKey
                );
            }
            catch (CosmosException ex)
            {
                _logger.LogError(ex.ToString());
                throw;
            }
        }

        public async Task<Message> UpdateMessageRatingAsync(string tenantId, string userId, string sessionId,string messageId, bool? rating)
        {
            try
            { 
                var partitionKey = PartitionManager.GetChatDataFullPK(tenantId, userId, sessionId);

                var response = await _chatData.PatchItemAsync<Message>(
                id: messageId,
                partitionKey: partitionKey,
                    patchOperations: new[]
                    {
                            PatchOperation.Set("/rating", rating),
                    }
                );
                return response.Resource;
            }
            catch (CosmosException ex)
            {
                _logger.LogError(ex.ToString());
                throw;
            }
        }

        public async Task<Session> UpdateSessionAsync(Session session)
        {
            try
            { 
                var partitionKey = PartitionManager.GetChatDataFullPK(session.TenantId, session.UserId, session.SessionId);

                return await _chatData.ReplaceItemAsync(
                    item: session,
                    id: session.Id,
                    partitionKey: partitionKey
                );
            }
            catch (CosmosException ex)
            {
                _logger.LogError(ex.ToString());
                throw;
            }
        }

        public async Task<Session> UpdateSessionNameAsync(string tenantId, string userId,string sessionId, string name)
        {
            try
            {
                var partitionKey = PartitionManager.GetChatDataFullPK(tenantId, userId, sessionId);

                var response = await _chatData.PatchItemAsync<Session>(
                    id: sessionId,
                    partitionKey: partitionKey,
                    patchOperations: new[]
                    {
                        PatchOperation.Set("/name", name),
                    }
                );


                return response.Resource;
            }
            catch (CosmosException ex)
            {
                _logger.LogError(ex.ToString());
                throw;
            }
}



        

        public async Task DeleteSessionAndMessagesAsync(string tenantId, string userId,string sessionId)
        {
            try
            { 
                var partitionKey = PartitionManager.GetChatDataFullPK(tenantId, userId, sessionId);

                var query = new QueryDefinition("SELECT c.id FROM c WHERE c.sessionId = @sessionId")
                    .WithParameter("@sessionId", sessionId);

                var response = _chatData.GetItemQueryIterator<Message>(query);

                var batch = _chatData.CreateTransactionalBatch(partitionKey);
                while (response.HasMoreResults)
                {
                    var results = await response.ReadNextAsync();
                    foreach (var item in results)
                    {
                        batch.DeleteItem(
                            id: item.Id
                        );
                    }
                }

                await batch.ExecuteAsync();
            }
            catch (CosmosException ex)
            {
                _logger.LogError(ex.ToString());
                throw;
            }
        }

        public async Task<DebugLog> GetChatCompletionDebugLogAsync(string tenantId, string userId,string sessionId, string debugLogId)
        {
            try
            { 
                var partitionKey = PartitionManager.GetChatDataFullPK(tenantId, userId, sessionId);

                return await _chatData.ReadItemAsync<DebugLog>(
                    id: debugLogId,
                    partitionKey: partitionKey);
            }
            catch (CosmosException ex)
            {
                _logger.LogError(ex.ToString());
                throw;
            }
        }
        

        public async Task<bool> InsertDocumentAsync(string containerName, JObject document)
        {
            Container container=null;

            switch (containerName)
            {
                case "OfferData":
                    container = _offersData;
                    break;
                case "AccountData":
                    container = _accountsData;
                    break;
                case "UserData":
                    container = _userData;
                    break;
            }
            try
            {

                // Insert cleaned document
                await container.CreateItemAsync(document);


                return true;
            }
            catch (CosmosException ex)
            {
                // Ignore conflict errors.
                if (ex.StatusCode == System.Net.HttpStatusCode.Conflict)
                {
                    _logger.LogInformation($"Duplicate document detected.");
                }
                else
                {
                    _logger.LogError(ex, "Error inserting document.");
                    throw;
                }
                return false;
            }
        }

        public async Task UpsertSessionBatchAsync(List<Message> messages, List<DebugLog> debugLogs, Session session)
        {
            try
            {
                if (messages.Select(m => m.SessionId).Distinct().Count() > 1 || session.SessionId != messages.Select(m => m.SessionId).FirstOrDefault())
                {
                    throw new ArgumentException("All items must have the same partition key.");
                }

                if (debugLogs.Count > 0 && (debugLogs.Select(m => m.SessionId).Distinct().Count() > 1 || session.SessionId != debugLogs.Select(m => m.SessionId).FirstOrDefault()))
                {
                    throw new ArgumentException("All items must have the same partition key as message.");
                }

                PartitionKey partitionKey = PartitionManager.GetChatDataFullPK(session.TenantId, session.UserId, session.SessionId);
                var batch = _chatData.CreateTransactionalBatch(partitionKey);
                foreach (var message in messages)
                {
                    batch.UpsertItem(
                        item: message
                    );
                }

                foreach (var log in debugLogs)
                {
                    batch.UpsertItem(
                        item: log
                    );
                }

                batch.UpsertItem(
                    item: session
                );

                await batch.ExecuteAsync();
            }
            catch (CosmosException ex)
            {
                _logger.LogError(ex.ToString());
                throw;
            }
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
    private readonly ICosmosDBService _cosmosDBService;
    private readonly ILogger _logger;
    private readonly IBankDataService _bankService;
    private readonly ISemanticKernelService _skService;

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
            //Replace from here
            ArgumentNullException.ThrowIfNull(sessionId);

            // Retrieve conversation, including latest prompt.
            var archivedMessages = await _cosmosDBService.GetSessionMessagesAsync(tenantId, userId, sessionId);

            // Add both prompt and completion to cache, then persist in Cosmos DB
            var userMessage = new Message(tenantId, userId, sessionId, "User", "User", userPrompt);

            // Generate the completion to return to the user
            var result = await _skService.GetResponse(userMessage, archivedMessages, tenantId, userId);

            await AddPromptCompletionMessagesAsync(tenantId, userId, sessionId, userMessage, result.Item1, result.Item2);

            return result.Item1;
            //end replace
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error getting completion in session {sessionId} for user prompt [{userPrompt}].");
            return new List<Message> { new Message(tenantId, userId, sessionId!,
                "Error", "Error", $"Error getting completion in session {sessionId} for user prompt [{userPrompt}].") };
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

## Next Steps

Proceed to Module 3: Agent Specialization
