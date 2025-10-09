

using Azure.AI.OpenAI;
using Azure.Identity;
using Banking.Services;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MultiAgentCopilot.Factories;
using MultiAgentCopilot.Models.Chat;
using MultiAgentCopilot.Models.Configuration;
using MultiAgentCopilot.Models.Debug;
using MultiAgentCopilot.MultiAgentCopilot.Factories;
using MultiAgentCopilot.MultiAgentCopilot.Helper;
using MultiAgentCopilot.MultiAgentCopilot.Services;
using OpenAI;
using OpenAI.Chat;
using System.Text.Json;
using ChatMessage = Microsoft.Extensions.AI.ChatMessage;

namespace MultiAgentCopilot.Services;

/// <summary>
/// Service responsible for managing AI agents and orchestrating multi-agent conversations
/// using the Microsoft Agents AI framework.
/// </summary>
public class AgentFrameworkService : IDisposable
{
    #region Private Fields
    
    private readonly AgentFrameworkServiceSettings _settings;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<AgentFrameworkService> _logger;
    private readonly IChatClient _chatClient;
    
    private BankingDataService? _bankService;
    private MCPToolService? _mcpService;
    private List<AIAgent>? _agents;
    private List<LogProperty> _promptDebugProperties;
    
    private bool _serviceInitialized = false;

    #endregion

    #region Properties

    public bool IsInitialized => _serviceInitialized;

    #endregion

    #region Constructor

    public AgentFrameworkService(
        IOptions<AgentFrameworkServiceSettings> options,
        ILoggerFactory loggerFactory)
    {
        _settings = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
        _logger = _loggerFactory.CreateLogger<AgentFrameworkService>();
        _promptDebugProperties = new List<LogProperty>();

        _chatClient = CreateChatClient();

        _logger.LogInformation("Agent Framework Initialized.");

        Task.Run(Initialize).ConfigureAwait(false);
    }

    #endregion

    #region Private Methods

    /// <summary>
    /// Creates and configures the Azure OpenAI chat client.
    /// </summary>
    private IChatClient CreateChatClient()
    {
        try
        {
            var credential = CreateAzureCredential();
            var endpoint = new Uri(_settings.AzureOpenAISettings.Endpoint);
            var openAIClient = new AzureOpenAIClient(endpoint, credential);
            
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

    /// <summary>
    /// Creates the appropriate Azure credential based on configuration.
    /// </summary>
    private DefaultAzureCredential CreateAzureCredential()
    {
        if (string.IsNullOrEmpty(_settings.AzureOpenAISettings.UserAssignedIdentityClientID))
        {
            return new DefaultAzureCredential();
        }
        
        return new DefaultAzureCredential(new DefaultAzureCredentialOptions
        {
            ManagedIdentityClientId = _settings.AzureOpenAISettings.UserAssignedIdentityClientID
        });
    }

    /// <summary>
    /// Logs messages for debugging purposes.
    /// </summary>
    private void LogMessage(string key, string value)
    {
        _promptDebugProperties.Add(new LogProperty(key, value));
    }

    /// <summary>
    /// Initializes the service asynchronously.
    /// </summary>
    private Task Initialize()
    {
        try
        {
            _serviceInitialized = true;
            _logger.LogInformation("Agent Framework service initialized.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Agent Framework service was not initialized. Error: {ErrorMessage}", ex.Message);
        }
        return Task.CompletedTask;
    }

    #endregion


    #region Public Methods

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

    /// <summary>
    /// Sets the MCP (Model Context Protocol) tool service.
    /// </summary>
    /// <param name="mcpService">The MCP tool service instance.</param>
    /// <returns>True if the service was set successfully.</returns>
    public bool SetMCPToolService(MCPToolService mcpService)
    {
        _mcpService = mcpService ?? throw new ArgumentNullException(nameof(mcpService));
        _logger.LogInformation("🔗 MCPToolService has been set - MCP integration ENABLED");
        return true;
    }



    /// <summary>
    /// Initializes the AI agents based on available tool services.
    /// </summary>
    /// <returns>True if agents were initialized successfully.</returns>
    public bool InitializeAgents()
    {
        try
        {

            if (_agents == null || _agents.Count == 0)
            {


                if (_mcpService != null)
                {
                    _agents = AgentFactory.CreateAllAgentsWithMCPToolsAsync(_chatClient, _mcpService, _loggerFactory).GetAwaiter().GetResult();
                }
                else if (_bankService != null)
                {
                    _agents = AgentFactory.CreateAllAgentsWithInProcessTools(_chatClient, _bankService, _loggerFactory);                    
                }
                else
                {
                    _logger.LogError("❌ No tool services available - cannot create agents");
                }

                if (_agents == null || _agents.Count == 0)
                {
                    _logger.LogError("💥 No agents available for orchestration");
                    return false;
                }

                _logger.LogInformation("🎯 Successfully initialized {AgentCount} agents", _agents.Count);
                
                // Log agent details
                foreach (var agent in _agents)
                {
                    _logger.LogInformation("🤖 Agent: {AgentName}, Description: {Description}", 
                        agent.Name, agent.Description);
                }
            }
            else
            {
                _logger.LogInformation("♻️ Agents already initialized ({AgentCount} agents available)", _agents.Count);
            }
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "💥 Failed to initialize agents: {ExceptionMessage}", ex.Message);
            return false;
        }
    }

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
            messageHistory.Add(userMessage);
            var chatHistory = ConvertToAIChatMessages(messageHistory);
            chatHistory.Add(new ChatMessage(ChatRole.User, userMessage.Text));

            _promptDebugProperties.Clear();


            var responseText = _agents[3].RunAsync(chatHistory).Result.Text;
            var selectedAgentName= _agents[3].Name;
            //var (responseText, selectedAgentName) = await RunGroupChatOrchestration(chatHistory, tenantId, userId);

            return CreateResponseTuple(userMessage, responseText, selectedAgentName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error when getting response: {ErrorMessage}", ex.Message);
            return new Tuple<List<Message>, List<DebugLog>>(new List<Message>(), new List<DebugLog>());
        }
    }

    /// <summary>
    /// Summarizes the given text.
    /// </summary>
    /// <param name="sessionId">The session identifier.</param>
    /// <param name="userPrompt">The text to summarize.</param>
    /// <returns>A summarized version of the text.</returns>
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

    /// <summary>
    /// Disposes of the service resources.
    /// </summary>
    public void Dispose()
    {
        _mcpService?.Dispose();
        GC.SuppressFinalize(this);
    }

    #endregion

    #region Private Helper Methods

    /// <summary>
    /// Converts message history to AI chat message format.
    /// </summary>
    private List<ChatMessage> ConvertToAIChatMessages(List<Message> messageHistory)
    {
        var chatHistory = new List<ChatMessage>();
        
        foreach (var msg in messageHistory)
        {
            var role = msg.SenderRole.ToLowerInvariant() switch
            {
                "user" => ChatRole.User,
                "assistant" => ChatRole.Assistant,
                "system" => ChatRole.System,
                _ => ChatRole.User
            };

            chatHistory.Add(new ChatMessage(role, msg.Text));
        }

        return chatHistory;
    }

    /// <summary>
    /// Creates a response tuple with message and debug log.
    /// </summary>
    private Tuple<List<Message>, List<DebugLog>> CreateResponseTuple(
        Message userMessage, 
        string responseText, 
        string selectedAgentName)
    {
        var completionMessages = new List<Message>();
        var completionMessagesLogs = new List<DebugLog>();

        string messageId = Guid.NewGuid().ToString();
        string debugLogId = Guid.NewGuid().ToString();

        var responseMessage = new Message(
            userMessage.TenantId,
            userMessage.UserId,
            userMessage.SessionId,
            selectedAgentName,
            "Assistant",
            responseText,
            messageId,
            debugLogId);

        completionMessages.Add(responseMessage);

        if (_promptDebugProperties.Count > 0)
        {
            var debugLog = new DebugLog(userMessage.TenantId, userMessage.UserId, userMessage.SessionId, messageId, debugLogId)
            {
                PropertyBag = _promptDebugProperties.ToList()
            };
            completionMessagesLogs.Add(debugLog);
        }

        return new Tuple<List<Message>, List<DebugLog>>(completionMessages, completionMessagesLogs);
    }

    /// <summary>
    /// Runs the workflow asynchronously and returns the response messages and selected agent.
    /// </summary>
    private async Task<(List<ChatMessage> messages, string selectedAgent)> RunWorkflowAsync(
        Workflow workflow, 
        List<ChatMessage> messages)
    {
        string? lastExecutorId = null;
        string selectedAgent = "Unknown";

        await using StreamingRun run = await InProcessExecution.StreamAsync(workflow, messages);
        await run.TrySendMessageAsync(new TurnToken(emitEvents: true));
        
        await foreach (WorkflowEvent evt in run.WatchStreamAsync().ConfigureAwait(false))
        {
            Console.WriteLine($"Event: {evt.GetType().Name}, data {evt.Data?.ToString()}");

            switch (evt)
            {
                case AgentRunUpdateEvent e when e.ExecutorId != lastExecutorId:
                    lastExecutorId = e.ExecutorId;
                    selectedAgent = ExtractAgentNameFromExecutorId(e.ExecutorId) ?? "Unknown";
                    LogAgentExecution(e);
                    break;
                    
                case WorkflowOutputEvent output:
                    return (output.As<List<ChatMessage>>()!, selectedAgent);
            }
        }

        return ([], selectedAgent);
    }

    /// <summary>
    /// Logs agent execution information.
    /// </summary>
    private void LogAgentExecution(AgentRunUpdateEvent agentEvent)
    {        
        // Check for function/tool calls
        var functionCalls = agentEvent.Update.Contents.OfType<FunctionCallContent>().ToList();
        if (functionCalls.Any())
        {
            _logger.LogInformation("🔥 TOOL CALLS DETECTED - {ToolCount} tool(s) being called!", functionCalls.Count);
            
            foreach (var call in functionCalls)
            {
                _logger.LogInformation("🎯 TOOL EXECUTION: Calling tool '{ToolName}' with arguments: {Arguments}", 
                    call.Name, 
                    JsonSerializer.Serialize(call.Arguments, new JsonSerializerOptions { WriteIndented = true }));
            }
        }
        
        // Check for function/tool results
        var functionResults = agentEvent.Update.Contents.OfType<FunctionResultContent>().ToList();
        if (functionResults.Any())
        {
            _logger.LogInformation("✅ TOOL RESULTS RECEIVED - {ResultCount} result(s)!", functionResults.Count);
            
            foreach (var result in functionResults)
            {
                _logger.LogInformation("✅ TOOL RESULT: Function {CallId} returned: {Result}", 
                    result.CallId, 
                    result.Result?.ToString() ?? "null");
            }
        }
        
        // If no function calls or results, log that too
        if (!functionCalls.Any() && !functionResults.Any())
        {
            _logger.LogInformation("ℹ️ No tool calls detected in this agent update - agent responded with text only");
        }
    }

    /// <summary>
    /// Extracts the agent name from the executor ID by removing the GUID suffix.
    /// </summary>
    private static string? ExtractAgentNameFromExecutorId(string? executorId)
    {
        if (string.IsNullOrEmpty(executorId))
            return null;

        var parts = executorId.Split('_');
        return parts.Length > 0 ? parts[0] : executorId;
    }

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
            _logger.LogInformation("Starting Agent Framework orchestration");
                       
            // Add system context
            chatHistory.Add(new ChatMessage(ChatRole.System, $"User Id: {userId}, Tenant Id: {tenantId}"));

            // Create custom termination function
            var customTerminationFunc = CreateCustomTerminationFunction();

            // Run the workflow
            var (responseMessages, selectedAgentName) = await RunWorkflowAsync(
                AgentWorkflowBuilder.CreateGroupChatBuilderWith(agents => 
                    new GroupChatOrchestrator(_agents!, _chatClient, LogMessage, customTerminationFunc) 
                    { 
                        MaximumIterationCount = 5                        
                    })
                    .AddParticipants(_agents!)
                    .Build(), 
                chatHistory);

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

    /// <summary>
    /// Creates a custom termination function for the group chat orchestrator.
    /// </summary>
    private Func<GroupChatOrchestrator, IEnumerable<ChatMessage>, CancellationToken, ValueTask<bool>> CreateCustomTerminationFunction()
    {
        return async (orchestrator, messages, token) =>
        {
            try
            {
                // The AI-based termination logic is implemented in the GroupChatOrchestrator
                // This function allows for additional business logic if needed
                return false; // Let the AI make the decision
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in custom termination function");
                return false; // Continue conversation on error
            }
        };
    }

    /// <summary>
    /// Extracts the response text from the last assistant message.
    /// </summary>
    private static string ExtractResponseText(List<ChatMessage> responseMessages)
    {
        if (responseMessages?.Any() == true)
        {
            var lastAssistantMessage = responseMessages.LastOrDefault(m => m.Role == ChatRole.Assistant);
            return lastAssistantMessage?.Text ?? "";
        }
        return "";
    }

    #endregion
}