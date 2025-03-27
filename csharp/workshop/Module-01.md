# Module 01 - Creating Your First Agent

[< Deployment and Setup](./Module-00.md) - **[Home](Home.md)** - [Connecting Agents to Memory >](./Module-02.md)

## Introduction

In this Module, you'll implement your first agent as part of a multi-agent banking system implemented using either Semantic Kernel Agent Framework. You will get an introduction to Semantic Kernel and their plug-in integration with OpenAI for generating completions.

## Learning Objectives and Activities

- Learn the basics for Semantic Kernel Agent Framework
- Learn how to integrate agent frameworks to Azure OpenAI
- Build a simple chat agent

## Module Exercises

1. [Activity 1: Session on Single-agent architecture](#activity-1-session-on-single-agent-architecture)
1. [Activity 2: Session on Semantic Kernel Agent Framework and LangGraph](#activity-2-session-on-semantic-kernel-agent-framework-and-langgraph)
1. [Activity 3: Instantiate Agent Framework and Connect to Azure OpenAI](#activity-3-instantiate-semantic-kernel-agent-framework-and-connect-to-azure-openai)
1. [Activity 4: Create a Simple Agent](#activity-4-create-a-simple-agent)
1. [Activity 5: Test your Work](#activity-5-test-your-work)

## Activity 1: Session on Single-agent architecture

In this session you will get an overview of Semantic Kernel Agents and LangGraph and learn the basics for how to build a chat app that interacts with a user and generations completions using an LLM powered by Azure OpenAI.

## Activity 2: Session on Semantic Kernel Agent Framework and LangGraph

In this session ou will get a deeper introduction into the Semantic Kernel Agent Framework and LangGraph with details on how to implement plug-in or tool integration with Azure Open AI.

## Activity 3: Instantiate Semantic Kernel Agent Framework and Connect to Azure OpenAI

In this hands-on exercise, you will learn how to initialize an agent framework and integrate it with a Large Language Model(LLM).

The Semantic Kernel Agent Framework is a platform within Microsoft's Semantic Kernel ecosystem designed to facilitate the creation and orchestration of AI agents. It enables developers to incorporate agentic patterns into applications, leveraging the core features of the Semantic Kernel framework. Agents built using this framework can collaborate, manage multiple concurrent conversations, and integrate human input, making it suitable for complex, multi-agent workflows.

There are a few key components and concepts necessary to build multi-agent apps using this framework.

- **AgentChat** - In the Semantic Kernel framework, AgentChat is an abstract class designed to facilitate interactions among multiple agents, even if they are of different types. This design allows, for example, a *ChatCompletionAgent* and an *OpenAIAssistantAgent* which are concrete implementations to collaborate within the same conversation. The AgentChat class defines entry points for initiating collaboration between agents, supporting scenarios where multiple responses or a single agent response are required. As an abstract class, AgentChat can be subclassed to support custom scenarios.

- **Functions** - Functions in Semantic Kernel are discrete units of work that can be executed within the AI application. They come in two primary forms:
  - **Native** - These are standard methods written in a programming language like C# or Python. They perform specific tasks, such as accessing a database, calling an external API, or processing data. To define a native function, you create a method in your codebase and annotate it with attributes that describe its purpose and parameters. This metadata allows the Semantic Kernel to understand and utilize the function appropriately.
  - **Semantic** - These functions are defined using natural language prompts and are designed to interact with LLMs. They guide the model to generate responses or perform tasks based on the provided prompt. Semantic functions are typically stored as text files, (for example, Prompty) containing the prompt and are loaded into the application at runtime. They can include variables and expressions to make the prompts dynamic and context-aware.

- **Plugins** - A plugin in Semantic Kernel is a collection of related functions—both native and semantic—that are grouped to provide a cohesive set of capabilities. Plugins serve as modular components that can be easily integrated into AI applications to extend their functionality.​ A plugin is typically implemented as a class containing multiple functions. Each function within the plugin is annotated with descriptive metadata, enabling the Semantic Kernel to understand its purpose and how to invoke it. Plugins encapsulate functionality that can be reused across different parts of an application or even across multiple applications. They promote modularity and maintainability by organizing related functions into a single, coherent unit. To use a plugin, it is imported into the Semantic Kernel, which then makes its functions available for invocation. This integration allows AI applications to dynamically utilize the plugin's capabilities as needed.

- **Connectors** - ​In the Semantic Kernel framework, Connectors serve as essential bridges that facilitate seamless integration between the kernel and various external services and AI models. These connectors enable developers to incorporate diverse AI functionalities—such as text generation, chat completion, embeddings, Vector Search, and more—into their applications without delving into the complexities of each AI provider's API. There are two types of connectors we will use in this workshop, AI Connectors and Vector Store Connectors.
  - **AI Connectors** - AI Connectors provide a uniform interface to interact with multiple AI services, such as Chat Completion and Embedding Generation, allowing developers to switch between different AI providers effortlessly. This flexibility is particularly beneficial for experimenting with various models to determine the best fit for specific use cases.
  - **Vector Store Connectors** - Beyond direct AI service integrations, Semantic Kernel provides connectors for various vector databases, including Azure Cosmos DB, facilitating tasks like semantic search and retrieval-augmented generation (RAG).

Let's dive into the starter solution for our workshop and get started completing the implementation for our multi-agent application.

The following steps are completed in your IDE.

### Project Structure

This solution is initially organized with the following three projects.

- **BankingServices** this project provides a data service layer for core the banking functionality for our application.
- **ChatAPI** this project wraps backend service layer for this solution. It exposes REST endpoints that are called by our frontend Angular app.
- **ChatInfrastructure** is the backend service layer itself. It provides the core wrappers for the underlying Azure Services and the Semantic Kernel Agent Framework and defines interfaces for each of the major service layers. Within this project the key entry point for the ChatAPI service above is the *ChatService*. All user interaction enters and leaves through this service.
- **Common** provides components that are shared across the entire solution including entity models, configuration, helpers as well as exception handling and debugging.

Here is what the structure of this solution appears like in Visual Studio. Spend a few moments to familiarize yourself with the structure as it will help you navigate as we go through the activities.

![Solution Files](./media/module-01/solution-files.png)

### Implement the SemanticKernelService

We are going to define an interface and the service layer for the Semantic Kernel Agent Framework that is central to our workshop. This interface defines the main functionality for our multi-agent service.

There are three functions we will implement as part of our multi-agent application.

- **GetResponse()** will be the entry point called by the front end to interact with the multi-agent service. Everything happens behind this function.
- **Summarize()** will be used to summarize the conversations users are having with the agent service.
- **GenerateEmbedding()** will be used to generate embeddings that are used to do vector searches for product data stored in Cosmos DB.

In your IDE, navigate to the **ChatInfrastructure** project in the solution.

Then navigate to the `\Interfaces\` folder within it.

Create a new class, **ISemanticKernelService.cs**

Replace the contents of the new class with the code below.

```csharp
using MultiAgentCopilot.Common.Models.Chat;
using MultiAgentCopilot.Common.Models.Debug;

namespace MultiAgentCopilot.ChatInfrastructure.Interfaces
{
    public interface ISemanticKernelService
    {
        Task<Tuple<List<Message>, List<DebugLog>>> GetResponse(Message userMessage, List<Message> messageHistory, string tenantId, string userId);
        Task<string> Summarize(string sessionId, string userPrompt);
        Task<float[]> GenerateEmbedding(string text);
    }
}
```

Next we will create the service class itself for Semantic Kernel that implements this interface.

Navigate to `ChatInfrastructure\Services\` in your solution.

Create a new class, **SemanticKernelService.cs**

Replace the contents of the new class with the code below.

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

    public async Task<Tuple<List<Message>, List<DebugLog>>> GetResponse(Message userMessage, List<Message> messageHistory,  string tenantId, string userId)
    {

        try
        {
            //GetResponse() code goes here

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

    public  async Task<float[]> GenerateEmbedding(string text)
    {
        // Generate Embedding
        var embeddingModel = _semanticKernel.Services.GetRequiredService<ITextEmbeddingGenerationService>();

        var embedding = await embeddingModel.GenerateEmbeddingAsync(text);

        // Convert ReadOnlyMemory<float> to IList<float>
       return embedding.ToArray();
    }
}
```

## Activity 4: Create a Simple Agent

In this activity we are going to create a simple customer service agent. This agent is going to be the key agent that will interact with users. However, right now, we are going to only implement a very simple agent that will take your user input and translate it into French. Later on we will greatly expand this customer service agent's functinality.

In Semantic Kernel all agents are derived from an abstract `Agent` class. Semantic Kernel provides a few built-in Agent classes including a ChatCompletionAgent, OpenAI Assistant Agent which are both derived by the `AgentChat` class in Semantic Kernel. There are also some experimental agents such as Azure AI Agent and Agent Collaboration. You can explore these on your own.

### Implement GetResponse() function

In our application the GetResponse() function is the key entry point for users.

In our example here we are going to implement a very simple **ChatCompletionAgent**. This is one of the built-in agents from Semantic Kernel Agent Framework and a key component for building these types of applications. The design for this agent is simple. It simply takes user input and translates the responses into French.

Within the `GetResponse()` function of  **SemanticKernelService.cs**, inside the `Try..Catch` block, copy the following code:

```csharp
            ChatCompletionAgent agent = new ChatCompletionAgent
            {
                Name = "BasicAgent",
                Instructions = "Greet the user and translate the request into French",
                Kernel = _semanticKernel.Clone()
            };

            ChatHistory chatHistory = [];

            chatHistory.AddUserMessage(userMessage.Text);

            _promptDebugProperties = new List<LogProperty>();

            List<Message> completionMessages = new();
            List<DebugLog> completionMessagesLogs = new();

            ChatMessageContent message = new(AuthorRole.User, userMessage.Text);
            chatHistory.Add(message);

            await foreach (ChatMessageContent response in agent.InvokeAsync(chatHistory))
            {
                string messageId = Guid.NewGuid().ToString();
                completionMessages.Add(new Message(userMessage.TenantId, userMessage.UserId, userMessage.SessionId, response.AuthorName ?? string.Empty, response.Role.ToString(), response.Content ?? string.Empty, messageId));
            }            
            return new Tuple<List<Message>, List<DebugLog>>(completionMessages, completionMessagesLogs);
```

### Add Dependency Injection for SemanticKernel

We next need to add some boiler plate code to our solution to support the new SemanticKernelService classes we've imlemented.

Navigate to `ChatInfrastructure\Services\`

Open the `DependencyInjection.cs` class. Copy the following code inside the class

```csharp
        /// <summary>
        /// Registers the <see cref="ISemanticKernelService"/> implementation with the dependency injection container.
        /// </summary>
        /// <param name="builder">The hosted applications and services builder.</param>
        public static void AddSemanticKernelService(this IHostApplicationBuilder builder)
        {
            builder.Services.AddOptions<SemanticKernelServiceSettings>()
                .Bind(builder.Configuration.GetSection("SemanticKernelServiceSettings"));
            builder.Services.AddSingleton<ISemanticKernelService, SemanticKernelService>();
        }
```

### Integrate SemanticKernelService to ChatService

As described earlier, the ChatService is the entry point for all user interaction with this application. The ChatAPI exposes all of the functions implemented by the ChatService and it subsequently calls every other servic within this application. Some of the functionality for this application we will implement here but that is largely centered on the user chat elements such as managing the chat history for the agents as well as integrating it into Semantic Kernel which it will forward requests on behalf of the user.

Let's begin to implement some of these elements for our application.

Within the same `ChatInfrastructure\Services\` folder, open the `ChatService.cs`

At the top of the `ChatService : IChatService` class, add the following line to the class declarations under `ICosmosDBService` and `ILogger`

```csharp
private readonly ISemanticKernelService _skService;
```

Next, replace the existing ChatService constructor to the following code:

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

### Integrate Semantic Kernel to Chat Service

In this step, you integrate the chat service to now call the `GetResponse()` method we pasted earlier for our a simple customer service agent.

Within the same `ChatService.cs` navigate to the `GetChatCompletionAsync()` function

Update the code within the function to this:

```csharp
    public async Task<List<Message>> GetChatCompletionAsync(string tenantId, string userId,string? sessionId, string userPrompt)
    {
        try
        {
            ArgumentNullException.ThrowIfNull(sessionId);
            
            // Add both prompt and completion to cache, then persist in Cosmos DB
            var userMessage = new Message(tenantId, userId, sessionId, "User", "User", userPrompt);

            // Generate the completion to return to the user
            var result = await _skService.GetResponse(userMessage, new List<Message>(), tenantId, userId);

            return result.Item1;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error getting completion in session {sessionId} for user prompt [{userPrompt}].");
            return new List<Message> { new Message(tenantId, userId, sessionId!, "Error", "Error", $"Error getting completion in session {sessionId} for user prompt [{userPrompt}].") };
        }
    }
```

This next function gets called by the service to rename chat session to give it a meaningful name. It does this by calling the semantic kernel service which passes the prompt text to an LLM and asks it to summarize the next. It then calls a second function that renames the chat and stores the new name in Cosmos DB.

In the next function below, `SummarizeChatSessionNameAsync()` replace the existing code for that function with this below.

```csharp
    public async Task<string> SummarizeChatSessionNameAsync(string tenantId, string userId,string? sessionId, string prompt)
    {
        try
        {
            ArgumentNullException.ThrowIfNull(sessionId);

            var summary = await _skService.Summarize(sessionId, prompt);

            var session = await RenameChatSessionAsync(tenantId, userId,sessionId, summary);

            return session.Name;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error getting a summary in session {sessionId} for user prompt [{prompt}].");
            return $"Error getting a summary in session {sessionId} for user prompt [{prompt}].";
        }
    }
```

### Final step, initialize the Semantic Kernel service

We are though all of the implementation elements for this module. The last step is to initialize the new SemanticKernelService we've just created.

In your IDE, navigate to the `ChatAPI` project. Then open `Program.cs`

Within the file, search for `builder.AddCosmosDBService();` and paste `builder.AddSemanticKernelService();` in the line below it so the code looks like this:

```csharp
builder.AddCosmosDBService();
builder.AddSemanticKernelService();
```

## Activity 5: Test your Work

With the activities in this module complete, it is time to test your work.

### Running the ChatAPI and Frontend App

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

1. Login to the frontend app.
1. Start a new conversation.
1. Send the message:  

   ```text
   Hello, how are you?
   ```

1. You should see something like the output below.

    ![Test output in French](./media/module-01/test-output.png)

### 4. Stop the Application

- In the frontend terminal, press **Ctrl + C** to stop the application.
- In your IDE press **Shift + F5** or stop the debugger.

### Validation Checklist

Your implementation is successful if:

- [ ] Your app compiles with no warnings or errors.
- [ ] Your agent successfully processes user input and generates and appropriate response.

### Common Issues and Troubleshooting

1. Frontend issues:
   - If frontend doesn't fully start, navigate to `/frontend/src/environments/environment.ts` and update `apiUrl: 'https://localhost:63279/'`
   - Frontend will restart

### Module Solution

The following sections include the completed code for this Module. Copy and paste these into your project if you run into issues and cannot resolve.

<details>
  <summary>Completed code for <strong>ChatInfrastructure/Interfaces/ISemanticKernelService.cs</strong></summary>
<br>

```csharp
using MultiAgentCopilot.Common.Models.Chat;
using MultiAgentCopilot.Common.Models.Debug;

namespace MultiAgentCopilot.ChatInfrastructure.Interfaces
{
    public interface ISemanticKernelService
    {
        Task<Tuple<List<Message>, List<DebugLog>>> GetResponse(Message userMessage, List<Message> messageHistory, string tenantId, string userId);
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
            ChatCompletionAgent agent = new ChatCompletionAgent
            {
                Name = "BasicAgent",
                Instructions = "Greet the user and translate the request into French",
                Kernel = _semanticKernel.Clone()
            };

            ChatHistory chatHistory = [];

            chatHistory.AddUserMessage(userMessage.Text);

            _promptDebugProperties = new List<LogProperty>();

            List<Message> completionMessages = new();
            List<DebugLog> completionMessagesLogs = new();

            ChatMessageContent message = new(AuthorRole.User, userMessage.Text);
            chatHistory.Add(message);

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

<details>
  <summary>Completed code for <strong>ChatInfrastructure/Services/DependencyInjection.cs</strong></summary>
<br>

```csharp
using MultiAgentCopilot.Common.Models.Configuration;
using MultiAgentCopilot.ChatInfrastructure.Interfaces;
using MultiAgentCopilot.ChatInfrastructure.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.ApplicationInsights.AspNetCore.Extensions;
using Microsoft.ApplicationInsights.Extensibility;
using Azure.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.ApplicationInsights;
namespace MultiAgentCopilot
{
    /// <summary>
    /// General purpose dependency injection extensions.
    /// </summary>
    public static partial class DependencyInjection
    {
        /// <summary>
        /// Registers the <see cref="ICosmosDBService"/> implementation with the dependency injection container.
        /// </summary>
        /// <param name="builder">The hosted applications and services builder.</param>
        public static void AddCosmosDBService(this IHostApplicationBuilder builder)
        {
            builder.Services.AddOptions<CosmosDBSettings>()
                .Bind(builder.Configuration.GetSection("CosmosDBSettings"));

            Console.WriteLine("Adding CosmosDBService:" + builder.Configuration["CosmosDBSettings:CosmosUri"]);
            builder.Services.AddSingleton<ICosmosDBService, CosmosDBService>();
        }


        /// <summary>
        /// Registers the <see cref="IChatService"/> implementation with the dependency injection container.
        /// </summary>
        /// <param name="builder">The hosted applications and services builder.</param>
        public static void AddChatService(this IHostApplicationBuilder builder)
        {
            builder.Services.AddOptions<CosmosDBSettings>()
                .Bind(builder.Configuration.GetSection("CosmosDBSettings"));
            builder.Services.AddSingleton<IChatService, ChatService>();
        }

        /// <summary>
        /// Registers the <see cref="ISemanticKernelService"/> implementation with the dependency injection container.
        /// </summary>
        /// <param name="builder">The hosted applications and services builder.</param>
        public static void AddSemanticKernelService(this IHostApplicationBuilder builder)
        {
            builder.Services.AddOptions<SemanticKernelServiceSettings>()
                .Bind(builder.Configuration.GetSection("SemanticKernelServiceSettings"));
            builder.Services.AddSingleton<ISemanticKernelService, SemanticKernelService>();
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
        ISemanticKernelService ragService,
        ILoggerFactory loggerFactory)
    {
        _cosmosDBService = cosmosDBService;
        _skService = ragService;
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

            // Add both prompt and completion to cache, then persist in Cosmos DB
            var userMessage = new Message(tenantId, userId, sessionId, "User", "User", userPrompt);

            // Generate the completion to return to the user
            var result = await _skService.GetResponse(userMessage, new List<Message>(), tenantId, userId);

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
}
```

</details>

<details>
  <summary>Completed code for <strong>ChatAPI/Program.cs</strong></summary>
<br>

```csharp
using MultiAgentCopilot;
using Microsoft.Extensions.Configuration;

namespace ChatAPI
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Load configuration from appsettings.json and environment variables
            builder.Configuration.SetBasePath(Directory.GetCurrentDirectory())
                                 .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                                 .AddJsonFile("appsettings.development.json", optional: true, reloadOnChange: true)
                                 .AddEnvironmentVariables();

            // Add CORS policy
            builder.Services.AddCors(options =>
            {
                options.AddPolicy("AllowAllOrigins",
                    policy =>
                    {
                        policy.AllowAnyOrigin()
                              .AllowAnyMethod()
                              .AllowAnyHeader();
                    });
            });


            builder.Logging.ClearProviders();
            builder.Logging.AddConsole();

            if (!builder.Environment.IsDevelopment())                
                builder.Services.AddApplicationInsightsTelemetry();

            builder.Logging.SetMinimumLevel(LogLevel.Trace);
            builder.Services.Configure<LoggerFilterOptions>(options =>
            {
                options.MinLevel = LogLevel.Trace;
            });

            //builder.AddApplicationInsightsTelemetry();

            builder.AddCosmosDBService();
            builder.AddSemanticKernelService();

            builder.AddChatService();
            builder.Services.AddScoped<ChatEndpoints>();

            // Add services to the container.
            builder.Services.AddAuthorization();

            // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();

            var app = builder.Build();
            app.UseCors("AllowAllOrigins");

            app.UseExceptionHandler(exceptionHandlerApp
                    => exceptionHandlerApp.Run(async context
                        => await Results.Problem().ExecuteAsync(context)));

            // Configure the HTTP request pipeline.
            app.UseSwagger();
            app.UseSwaggerUI();

            app.UseAuthorization();

            // Map the chat REST endpoints:
            using (var scope = app.Services.CreateScope())
            {
                var service = scope.ServiceProvider.GetService<ChatEndpoints>();
                service?.Map(app);
            }

            app.Run();
        }
    }
}
```

</details>

## Next Steps

Proceed to [Connecting Agents to Memory](./Module-02.md)

## Resources

- [Semantic Kernel Agent Framework](https://learn.microsoft.com/semantic-kernel/frameworks/agent)
- [Azure OpenAI Service documentation](https://learn.microsoft.com/azure/cognitive-services/openai/)
- [Azure Cosmos DB Vector Database](https://learn.microsoft.com/azure/cosmos-db/vector-database)
