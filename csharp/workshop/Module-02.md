# Module 02 - Connecting Agents to Memory

[< Creating Your First Agent](./Module-01.md) - **[Home](Home.md)** - [Agent Specialization >](./Module-03.md)

## Introduction

In this Module you'll connect your agent to Azure Cosmos DB to provide memory for chat history and state management for your agents to provide durability and context-awareness in your agent interactions.


## Learning Objectives and Activities

- Learn the basics for Azure Cosmos DB for storing state and chat history
- Learn how to integrate agent frameworks to Azure Cosmos DB
- Test connectivity to Azure Cosmos DB works

## Module Exercises

1. [Activity 1: Session Memory Persistence in Agent Frameworks](#activity-1-session-memory-persistence-in-agent-frameworks)
1. [Activity 2: Create a Simple Agent](#activity-2-create-a-simple-agent)
1. [Activity 3: Connecting Agent Frameworks to Azure Cosmos DB](#activity-2-connecting-agent-frameworks-to-azure-cosmos-db)
1. [Activity 4: Test your Work](#activity-5-test-your-work)


## Activity 1: Session Memory Persistence in Agent Frameworks

In this session you will get an overview of memory and how it works for Semantic Kernel Agents and LangGraph and learn the basics for how to configure and connect both to Azure Cosmos DB as a memory store for both chat history and/or state management.


## Activity 2: Create a Simple Agent

In this hands-on exercise, you will learn how to create a agent using a simple prompt.

Create `Factories` folder inside ChatInfrastructure.

### Add SystemPromptFactory.cs in ChatInfrastructure\Factories

System prompts provide instructions to the LLM, shaping its responses accordingly. The `SystemPromptFactory` class enables the creation of system prompts for various scenarios.

```csharp

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.SemanticKernel;
using MultiAgentCopilot.ChatInfrastructure.Services;


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

### Add ChatFactory.cs in \ChatInfrastructure\Factories

Agents are autonomous systems that use LLMs to process inputs, make decisions, and generate responses based on predefined goals. They can integrate with external tools, retrieve information, and adapt dynamically to different tasks. The `ChatFactory` class enables the creation of agents for various scenarios. It uses the SystemPromptFactory to define the agent prompts.

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

Now we have an agent that executes the instructions provided as prompts. Next lets add some memory so that the agent can remember the previous messages and doesn't loose context of the chat session.


## Activity 3: Connecting Agent Frameworks to Azure Cosmos DB

In this hands-on exercise, you will learn how to initialize Azure Cosmos DB and integrate with an agent framework to provide persistent memory for chat history and state management.

Add  MultiAgentCopilot.ChatInfrastructure.Factories reference  in ChatInfrastructure\Services\SemanticKernelService.cs

```csharp

using MultiAgentCopilot.ChatInfrastructure.Factories;

```


### Update GetResponse in ChatInfrastructure\Services\SemanticKernelService.cs

The `messageHistory` list stores historical messages from the chat session. The `chatHistory` object is used to construct this history and invoke the agent. The `completionMessages` list is used to store the response received from the agent into CosmosDB for next iteration.

```csharp

    public async Task<Tuple<List<Message>, List<DebugLog>>> GetResponse(Message userMessage, List<Message> messageHistory,  string tenantId, string userId)
    {

        try
        {
            ChatFactory agentChatGeneratorService = new ChatFactory();

            var agent = agentChatGeneratorService.BuildAgent(_semanticKernel, _loggerFactory,  tenantId, userId);

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

### Store the user message and responses into CosmosDB for easy retrieval of history later

Add UpsertSessionBatchAsync to ChatInfrastructure\Interfaces\ICosmosDBService.cs

```csharp

Task UpsertSessionBatchAsync(List<Message> messages, List<DebugLog> debugLogs, Session session);

```

Add UpsertSessionBatchAsync to ChatInfrastructure\Services\CosmosDbService.cs

The user message and each response (can be multiple) is stored as a separate document in Cosmos DB, but they all share the same partition key.

```csharp

        public async Task UpsertSessionBatchAsync(List<Message> messages, List<DebugLog>debugLogs, Session session)
        {
            try
            { 
                if (messages.Select(m => m.SessionId).Distinct().Count() > 1 || session.SessionId != messages.Select(m => m.SessionId).FirstOrDefault())
                {
                    throw new ArgumentException("All items must have the same partition key.");
                }

                if (debugLogs.Count>0 && (debugLogs.Select(m => m.SessionId).Distinct().Count() > 1 || session.SessionId != debugLogs.Select(m => m.SessionId).FirstOrDefault()))
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

```

Add AddPromptCompletionMessagesAsync to ChatInfrastructure\Services\ChatService.cs

Update the chat Service to store the chat responses.

```csharp
    /// <summary>
    /// Add user prompt and AI assistance response to the chat session message list object and insert into the data service as a transaction.
    /// </summary>
    private async Task AddPromptCompletionMessagesAsync(string tenantId, string userId,string sessionId, Message promptMessage, List<Message> completionMessages, List<DebugLog> completionMessageLogs)
    {
        var session = await _cosmosDBService.GetSessionAsync(tenantId, userId,sessionId);

        completionMessages.Insert(0, promptMessage);
            await _cosmosDBService.UpsertSessionBatchAsync(completionMessages, completionMessageLogs, session);
    }
```

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
            var result = await _skService.GetResponse(userMessage, archivedMessages,tenantId,userId);

            await AddPromptCompletionMessagesAsync(tenantId, userId,sessionId, userMessage, result.Item1, result.Item2);

            return result.Item1;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error getting completion in session {sessionId} for user prompt [{userPrompt}].");
#pragma warning disable CS8603 // Possible null reference return.
            return null;
#pragma warning restore CS8603 // Possible null reference return.
        }
    }

```

## Activity 4: Test your Work

With the hands-on exercises complete it is time to test your work.


### Running the ChatAPI and Frontend App

#### 1. Start the ChatAPI

##### If running on Codespaces:
1. Navigate to `src\ChatAPI`.
2. Run the following command to trust the development certificate:
   ```sh
   dotnet dev-certs https --trust
   ```
3. Start the application:
   ```sh
   dotnet run
   ```
4. Copy the URL from the **Ports** tab.

##### If running locally on Visual Studio or VS Code:
1. Navigate to `src\ChatAPI`.
2. Press **F5** or select **Run** to start the application.
3. Copy the URL from the browser window that opens.

#### 2. Run the Frontend App
- Follow the [README instructions](../../README.md) to start the frontend application.  
- Use the URL copied in the previous step as the API endpoint.

#### 3. Start a Chat Session
1. Open the frontend app.
2. Start a new chat session.
3. Send the following message:  
   ```
   Can a senior citizen open a savings account?
   ```
4. Wait for the Agent response.
5. Send another message:  
   ```
   Does the interest rate vary?
   ```
6. Expected response: The Agent's response is contextually correct for the  whole chat session.

### 4. Stop the Application
- Press **Ctrl + C** to stop the debugger.


### Validation Checklist

Your implementation is successful if:

- [ ] Your app compiles with no warnings or errors.
- [ ] Your agent successfully connects to Azure Cosmos DB. (**TBD how do we test this?**)


### Common Issues and Troubleshooting

1. Issue 1:
    - TBD
    - TBD

1. Issue 2:
    - TBD
    - TBD

1. Issue 3:
    - TBD
    - TBD


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

Proceed to [Agent Specialization](./Module-03.md)

## Resources

- [Semantic Kernel Agent Framework](https://learn.microsoft.com/semantic-kernel/frameworks/agent)
- [LangGraph](https://langchain-ai.github.io/langgraph/concepts/)
- [Azure OpenAI Service documentation](https://learn.microsoft.com/azure/cognitive-services/openai/)
- [Azure Cosmos DB Vector Database](https://learn.microsoft.com/azure/cosmos-db/vector-database)
