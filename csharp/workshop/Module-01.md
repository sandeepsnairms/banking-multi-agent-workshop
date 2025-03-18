# Module 01 - Creating Your First Agent

[< Deployment and Setup](./Module-00.md) - **[Home](Home.md)** - [Connecting Agents to Memory >](./Module-02.md)

## Introduction

In this Module, you'll implement your first agent as part of a multi-agent banking system implemented using either Semantic Kernel Agent Framwork or LangGraph. You will get an introduction to Semantic Kernel and LangChain frameworks and their plug-in/tool integration with OpenAI for generating completions.

## Learning Objectives and Activities

- Learn the basics for Semantic Kernel Agent Framework and LangGraph
- Learn how to integrate agent framworks to Azure OpenAI
- Build a simple chat agent

## Module Exercises

1. [Activity 1: Session on Single-agent architecture](#activity-1-session-on-single-agent-architecture)
1. [Activity 2: Session on Semantic Kernel Agent Framework and LangGraph](#activity-2-session-on-semantic-kernel-agent-framework-and-langgraph)
1. [Activity 3: Instantiate Agent Framework and Connect to Azure OpenAI](#activity-3-instantiate-agent-framework-and-connect-to-azure-openai)
1. [Activity 4: Create a Simple Customer Service Agent](#activity-4-create-a-simple-customer-service-agent)
1. [Activity 5: Test your Work](#activity-5-test-your-work)

## Activity 1: Session on Single-agent architecture

In this session you will get an overview of Semantic Kernel Agents and LangGraph and learn the basics for how to build a chat app that interacts with a user and generations completions using an LLM powered by Azure OpenAI.

## Activity 2: Session on Semantic Kernel Agent Framework and LangGraph

In this session ou will get a deeper introduction into the Semantic Kernel Agent Frameowkr and LangGraph with details on how to implement plug-in or tool integration with Azure Open AI.


## Activity 3: Instantiate Agent Framework and Connect to Azure OpenAI

In this hands-on exercise, you will learn how to initialize an agent framework and integrate it with a large langugage model.

### Add SemanticKernelService


### Add SemanticKernelServiceSettings Config

Add **AzureOpenAISettings.cs**  to **Common\Models\Configuration\**

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MultiAgentCopilot.Common.Models.Configuration
{
    public record AzureOpenAISettings
    {
        public required string Endpoint { get; init; }

        public  string? Key { get; init; }           

        public required string CompletionsDeployment { get; init; }

        public required string EmbeddingsDeployment { get; init; }
        
        public required string UserAssignedIdentityClientID { get; init; }
    }
}

```


Add **SemanticKernelServiceSettings.cs** to **Common\Models\Configuration\**
```csharp

namespace MultiAgentCopilot.Common.Models.Configuration
{
    public record SemanticKernelServiceSettings
    {
        public required AzureOpenAISettings AzureOpenAISettings { get; init; }
        public required CosmosDBSettings CosmosDBVectorStoreSettings { get; init; }
    }
}

```



Add **ISemanticKernelService.cs** to **ChatInfrastructure\Interfaces\**

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

Add **SemanticKernelService.cs** to **ChatInfrastructure\Services\**

```csharp

using MultiAgentCopilot.Common.Models.Chat;
using MultiAgentCopilot.ChatInfrastructure.Interfaces;
using MultiAgentCopilot.Common.Models.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using MultiAgentCopilot.Common.Models.Configuration;
using Microsoft.SemanticKernel.ChatCompletion;

using Azure.Identity;
using Newtonsoft.Json;
using System.Data;
using MultiAgentCopilot.Common.Models.Debug;
using Microsoft.SemanticKernel.Embeddings;
using System.Runtime;
using Microsoft.SemanticKernel.Agents;
using Message = MultiAgentCopilot.Common.Models.Chat.Message;
using System;


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

     ChatHistory chatHistory = [];
    public async Task<Tuple<List<Message>, List<DebugLog>>> GetResponse(Message userMessage, List<Message> messageHistory,  string tenantId, string userId)
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

Study and understand the following from the code above:

- Constructor – Examine how the class is initialized and what dependencies or parameters it requires.
- GetResponse function – Analyze its purpose, input parameters, processing logic, and return value.
- Summarize function – Understand how it processes data, its algorithm, and the expected output.

### Add SK Dependency

Update **ChatInfrastructure\Services\DependencyInjection.cs**, add the following function to **DependencyInjection** class

```c#
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

#### Integrate SemanticKernelService to ChatService

Update ChatInfrastructure\Services\ChatService.cs

Create ISemanticKernelService object

```csharp
private readonly ISemanticKernelService _skService;

```

Update ChatService constructor to add SemanticKernelServiceSettings

```csharp
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

```

In this hands-on exercise, you will create a simple customer service agent that users interact with and generates completions using a large language model.

### Integrate SK to Chat Service

Update GetChatCompletionAsync to

```csharp
 public async Task<List<Message>> GetChatCompletionAsync(string tenantId, string userId,string? sessionId, string userPrompt)
    {
        try
        {
            ArgumentNullException.ThrowIfNull(sessionId);
            

            // Add both prompt and completion to cache, then persist in Cosmos DB
            var userMessage = new Message(tenantId,userId,sessionId, "User","User", userPrompt);

            // Generate the completion to return to the user
            var result = await _skService.GetResponse(userMessage, new List<Message>(), tenantId,userId);

            

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

Update SummarizeChatSessionNameAsync to

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
#pragma warning disable CS8603 // Possible null reference return.
            return null;
#pragma warning restore CS8603 // Possible null reference return.
        }

    }

```

Add  ISemanticKernelService interface  as Singleton to ChatAPI\Program.cs

Search for builder.AddCosmosDBService(), paste the below line.

```csharp

builder.AddSemanticKernelService();

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
5. Send the message.
6. Expected response is a greeting along with the message you sent translated  in French.
7. Select ctrl+ C to stop the debugger.

### Validation Checklist

Your implementation is successful if:

- [ ] Your app compiles with no warnings or errors.
- [ ] Your agent successfully processes user input and generates and appropriate response.

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

Proceed to [Connecting Agents to Memory](./Module-02.md)

## Resources

- [Semantic Kernel Agent Framework](https://learn.microsoft.com/semantic-kernel/frameworks/agent)
- [LangGraph](https://langchain-ai.github.io/langgraph/concepts/)
- [Azure OpenAI Service documentation](https://learn.microsoft.com/azure/cognitive-services/openai/)
- [Azure Cosmos DB Vector Database](https://learn.microsoft.com/azure/cosmos-db/vector-database)
