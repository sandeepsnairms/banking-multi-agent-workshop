# Module 01 - Creating Your First Agent

## Introduction

In this module, you'll implement your first agent as part of a multi-agent banking system using Microsoft Agent Framework. You will get an introduction to the Microsoft Extensions AI framework and their integration with Azure OpenAI for generating intelligent responses in conversational interfaces.

## Learning Objectives

- Learn the fundamentals of Microsoft Agent Framework and Extensions AI
- Understand how to integrate agent frameworks with Azure OpenAI services
- Build and configure a simple chat agent with basic conversational capabilities
- Implement agent response handling and message processing

## Module Exercises

1. [Activity 1: Instantiate Agent Framework](#activity-1-instantiate-agent-framework)
1. [Activity 2: Create a Simple Agent](#activity-2-create-a-simple-agent)
1. [Activity 3: Test your Work](#activity-3-test-your-work)

## Activity 1: Instantiate Agent Framework

In this hands-on exercise, you will learn how to initialize an agent framework and integrate it with a Large Language Model (LLM).

The Microsoft Agent Framework is built on top of Microsoft Extensions AI, a unified API layer that provides a consistent interface for AI services. It enables developers to incorporate agentic patterns into applications by abstracting the complexities of different AI providers and offering a standardized way to build intelligent agents. Agents built using this framework can collaborate, manage multiple concurrent conversations, and integrate human input, making it suitable for complex, multi-agent workflows.

There are several key components and concepts necessary to build multi-agent apps using this framework:

- **IChatClient** - The core interface in Microsoft Extensions AI that provides a unified way to interact with different chat completion services. It abstracts the underlying AI provider (like Azure OpenAI, OpenAI, or others) and provides a consistent API for sending messages and receiving responses. The chat client handles the communication protocol, authentication, and request/response formatting, allowing developers to focus on building agent logic rather than managing provider-specific APIs.

- **AIAgent** - A higher-level abstraction built on top of IChatClient that represents an individual agent with specific behavior, personality, and capabilities. An AIAgent encapsulates the agent's instructions (system prompt), tools/functions it can use, and conversation handling logic. It provides methods like RunAsync() to process user input and generate responses based on the agent's configuration.

- **Tools and Functions** - Tools in the Microsoft Agent Framework are callable functions that extend an agent's capabilities beyond text generation. They come in two primary forms:
  - **Native Functions** - These are standard C# methods that perform specific tasks, such as accessing a database, calling an external API, or processing data. To define a native function, you create a method in your codebase and annotate it with attributes that describe its purpose and parameters. This metadata allows the agent framework to understand and utilize the function appropriately.
  - **Semantic Functions** - These functions are defined using natural language prompts and are designed to interact with LLMs for specific tasks. They guide the model to generate responses or perform tasks based on the provided prompt. Semantic functions can include variables and expressions to make the prompts dynamic and context-aware.

- **Agent Groups and Orchestration** - The framework supports multi-agent scenarios where multiple agents can collaborate on complex tasks. Agent groups allow for orchestration patterns where agents can hand off conversations, work in parallel, or follow specific workflows. This enables building sophisticated AI systems where different agents specialize in different domains or capabilities.

- **Connectors and Integrations** - The Microsoft Extensions AI framework provides connectors that facilitate seamless integration with various external services and AI models. These connectors enable developers to incorporate diverse AI functionalities—such as text generation, chat completion, embeddings, and vector search—into their applications without dealing with provider-specific APIs. There are two types of connectors we will use in this workshop:
  - **AI Service Connectors** - These provide a uniform interface to interact with multiple AI services, allowing developers to switch between different AI providers effortlessly. This flexibility is particularly beneficial for experimenting with various models.
  - **Vector Store Connectors** - Beyond direct AI service integrations, the framework provides connectors for various vector databases, including Azure Cosmos DB, facilitating tasks like semantic search and retrieval-augmented generation (RAG).

Let's dive into the starter solution for our workshop and get started completing the implementation for our multi-agent application.

### Implement the AgentFrameworkService

We are going to define two primary functions as part of our multi-agent application:

- **GetResponse()** will be the entry point called by the front end to interact with the multi-agent service. Everything happens behind this function.
- **Summarize()** will be used to summarize the conversations users are having with the agent service.

1. In VS Code, use the explorer on the left-hand side of the IDE to open the **MultiAgentCopilot\Services** folder.
1. Within the **\Services** folder navigate to **AgentFrameworkService.cs**.
1. Search for **//TO DO: CreateChatClient** and replace **CreateChatClient()** method with the code below.

**Note:** To paste code, place your cursor exactly where you want it in the code, including any tabs or spaces, then click the `T` in the lab guide. This will paste the code directly into your app. You may need to tab or format the code a little after pasting.

This method creates and configures an Azure OpenAI chat client with proper authentication. It uses DefaultAzureCredential for seamless authentication across different environments.

```csharp
        private IChatClient CreateChatClient()
        {
            try
            {
                var credential = CreateAzureCredential();
                var endpoint = new Uri(_settings.AzureOpenAISettings.Endpoint);
                var openAIClient = new AzureOpenAIClient(endpoint, credential, new AzureOpenAIClientOptions
                {
                    Transport = new HttpClientPipelineTransport(),
                });
        
                return openAIClient
                    .GetChatClient(_settings.AzureOpenAISettings.CompletionsDeployment)
                    .AsIChatClient();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create chat client");
                throw;
            }
        }

```

## Activity 2: Create a Simple Agent

Let's create a very simple agent for our workshop that is powered by an LLM. This agent will simply greet users and translate their requests into French. We will also implement a summarize function that renames the current chat session based upon the current topic from the user.

1. Search for **//TO DO: Add GetResponse function** and replace **GetResponse()** method with the code below.

This method creates a simple AI agent with specific instructions and runs it with user input. The agent is configured to greet users and translate requests into French.


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
            var agent = _chatClient.CreateAIAgent(
                "Greet the user and translate the request into French",
                "Translator");
            
    
            var responseText= agent.RunAsync(userMessage.Text).GetAwaiter().GetResult().Text;
            return CreateResponseTuple(userMessage, responseText, "Translator");      
    
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error when getting response: {ErrorMessage}", ex.Message);
            return new Tuple<List<Message>, List<DebugLog>>(new List<Message>(), new List<DebugLog>());
        }
    }
```

1. Search for **//TO DO: Add Summarize function** and replace **Summarize()** method with the code below.

This method creates a summarization agent that condenses user input into exactly two words. It's useful for generating concise session titles or quick content summaries.

```csharp

public async Task<string> Summarize(string sessionId, string userPrompt)
{
    try
    {
        var agent = _chatClient.CreateAIAgent(
            "Summarize the text into exactly two words:", 
            "Summarizer");

        return agent.RunAsync(userPrompt).GetAwaiter().GetResult().Text;        
      
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error when getting response: {ErrorMessage}", ex.Message);
        return string.Empty;
    }
}   

```

### Update ChatService

1. Remain in the **/Services** folder
1. Navigate to **ChatService.cs**.
1. Replace the code for **GetChatCompletionAsync()** method with code below.

This method integrates the agent framework service with the chat service. It processes user messages and returns the agent's response through the framework.

```csharp
    public async Task<List<Message>> GetChatCompletionAsync(string tenantId, string userId, string? sessionId, string userPrompt)
    {
        try
        {
            var archivedMessages = new List<Message>();
            var userMessage = new Message(tenantId, userId, sessionId, "User", "User", userPrompt); 
            var result = await _afService.GetResponse(userMessage, archivedMessages, _bankService, tenantId, userId);
            return result.Item1;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error getting completion in session {sessionId} for user prompt [{userPrompt}].");
            return new List<Message> { new Message(tenantId, userId, sessionId!, "Error", "Error", $"Error getting completion in session {sessionId} for user prompt [{userPrompt}].") };
        }
    }

```


## Activity 3: Test your Work

With the activities in this module complete, it is time to test your work. The agent should respond and greet you then translate your request into French.

### Start the Backend

1. Return to the open terminal for the backend app in VS Code. Ensure you are in `01_exercises\csharp\src\MultiAgentCopilot` folder. Type `dotnet run`

### Start a Chat Session

1. Return to the still running frontend application in your browser.
1. Send the message:  

```text
Hello, how are you?
```

1. You should see something like the output below.

    ![Test output in French](./media/module-01/test-output.png)

### Stop the Application

1. Return to VS Code.
1. Select the backend terminal, press **Ctrl + C** to stop the backend application.

## Validation Checklist

Your implementation is successful if:

- [ ] Your app compiles with no errors.
- [ ] Your agent successfully processes user input and generates an appropriate response.

## Module Solution

The following sections include the completed code for this Module. Copy and paste these into your project if you run into issues and cannot resolve.

<details>
  <summary>Completed code for <strong>\Services\AgentFrameworkService.cs</strong></summary>
<br>

```csharp


```

<details>
  <summary>Completed code for <strong>\Services\ChatService.cs</strong></summary>
<br>

```csharp


```

</details>

## Next Steps

Proceed to Module 2: [Connecting Agents to Memory](./Module-02.md)
