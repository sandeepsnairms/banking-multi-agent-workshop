
using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Agents.Orchestration;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.AI.Agents;
using Microsoft.Extensions.AI.Agents.OpenAI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MultiAgentCopilot.Factories;
using MultiAgentCopilot.Models;
using MultiAgentCopilot.Models.Chat;
using MultiAgentCopilot.Models.ChatInfoFormats;
using MultiAgentCopilot.Models.Configuration;
using MultiAgentCopilot.Models.Debug;
using MultiAgentCopilot.MultiAgentCopilot.Factories;
using MultiAgentCopilot.Services;
using MultiAgentCopilot.StructuredFormats;
using OpenAI;
//using Azure.AI.OpenAI;
using OpenAI.Chat;
using System.Text.Json;
//using Azure.AI.OpenAI;


namespace MultiAgentCopilot.Services;

public class AgentFrameworkService : IDisposable
{
    private readonly AgentFrameworkServiceSettings _settings;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<AgentFrameworkService> _logger;
    private readonly Microsoft.Extensions.AI.IChatClient _chatClient;

    private bool _serviceInitialized = false;
    private List<LogProperty> _promptDebugProperties;

    public bool IsInitialized => _serviceInitialized;

    public AgentFrameworkService(
        IOptions<AgentFrameworkServiceSettings> options,
        ILoggerFactory loggerFactory)
    {
        _settings = options.Value;
        _loggerFactory = loggerFactory;
        _logger = _loggerFactory.CreateLogger<AgentFrameworkService>();
        _promptDebugProperties = new List<LogProperty>();

        _logger.LogInformation("Initializing the Agent Framework service...");

        // Create Azure OpenAI chat client with improved credential handling

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


        var endpoint = new Uri(_settings.AzureOpenAISettings.Endpoint);

        var openAIClient = new AzureOpenAIClient(endpoint, credential);
        // Use ChatClient directly from OpenAI SDK
        _chatClient = openAIClient.GetChatClient(_settings.AzureOpenAISettings.CompletionsDeployment).AsIChatClient();

        // Initialize the placeholder orchestration (should be REAL Agent Framework)
        //_orchestration = new AgentFrameworkOrchestration(
        //    _chatClient,
        //    (BankingDataService) null, // Provide a non-null instance as required by the constructor
        //    _loggerFactory,
        //    "Contoso",
        //    "Mark"
        //);
            
        _logger.LogWarning("Using placeholder orchestration. Microsoft Agent Framework GroupChatOrchestration is needed.");

        Task.Run(Initialize).ConfigureAwait(false);
    }

    private void LogMessage(string key, string value)
    {
        _promptDebugProperties.Add(new LogProperty(key, value));
    }

    public async Task<Tuple<List<Message>, List<DebugLog>>> GetResponse(
        Message userMessage,
        List<Message> messageHistory,
        BankingDataService bankService,
        string tenantId,
        string userId)
    {
        try
        {
            // Convert message history to ChatMessage format
            var chatHistory = new List<Microsoft.Extensions.AI.ChatMessage>();
            foreach (var msg in messageHistory)
            {
                var role = msg.SenderRole.ToLowerInvariant() switch
                {
                    "user" => ChatMessageRole.User,
                    "assistant" => ChatMessageRole.Assistant,
                    "system" => ChatMessageRole.System,
                    _ => ChatMessageRole.User
                };

                if (role == ChatMessageRole.User)
                {
                    chatHistory.Add(new Microsoft.Extensions.AI.ChatMessage(ChatRole.User, msg.Text));
                }
                else if (role == ChatMessageRole.Assistant)
                {
                    chatHistory.Add(new Microsoft.Extensions.AI.ChatMessage(ChatRole.Assistant, msg.Text));
                }
                else if (role == ChatMessageRole.System)
                {
                    chatHistory.Add(new Microsoft.Extensions.AI.ChatMessage(ChatRole.System, msg.Text));
                }
            }

            // Add user message
            chatHistory.Add(new Microsoft.Extensions.AI.ChatMessage(ChatRole.User, userMessage.Text));

            _promptDebugProperties.Clear();

            // Use AI-based GroupChat orchestration with selection and termination strategies
            var (responseText, selectedAgentName) = await RunGroupChatOrchestration(chatHistory, bankService, tenantId, userId);

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
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error when getting response: {ErrorMessage}", ex.ToString());
            return new Tuple<List<Message>, List<DebugLog>>(new List<Message>(), new List<DebugLog>());
        }
    }

    private async Task<(string responseText, string selectedAgentName)> RunGroupChatOrchestration(
        List<Microsoft.Extensions.AI.ChatMessage> chatHistory,
        BankingDataService bankService,
        string tenantId,
        string userId)
    {
        try
        {
            _logger.LogInformation("Starting Agent Framework orchestration");


            OrchestrationMonitor monitor = new();
            monitor.History.AddRange(chatHistory);

            // Define the orchestration
            //ConcurrentOrchestration orchestration =
            //    new(AgentFactory.CreateAllAgents(_chatClient, bankService, tenantId, userId, _loggerFactory).ToArray())
            //    {
            //        LoggerFactory = this._loggerFactory,
            //        ResponseCallback = monitor.ResponseCallbackAsync,
            //        StreamingResponseCallback =  monitor.StreamingResultCallbackAsync
            //    };

                       
                
            // Create a custom GroupChatManager with SelectionStrategy and TerminationStrategy
            var groupChatManager = new BankingGroupChatManager(chatHistory.Last().Text, _chatClient);

            GroupChatOrchestration groupChatOrchestration = new GroupChatOrchestration(groupChatManager, AgentFactory.CreateAllAgents(_chatClient, bankService, tenantId, userId, _loggerFactory).ToArray());
            //{               
            //    LoggerFactory = this._loggerFactory,
            //    ResponseCallback = monitor.ResponseCallbackAsync,
            //    StreamingResponseCallback = monitor.StreamingResultCallbackAsync
            //};

            var orchestrationResponse = await groupChatOrchestration.RunAsync(chatHistory);
            AgentRunResponse result = await orchestrationResponse.Task;

            string responseText = result.Text;
            // In RunGroupChatOrchestration, ensure selectedAgentName is not null
            string selectedAgentName = result.AgentId ?? "Unknown";

            _logger.LogInformation("Agent Framework orchestration completed with agent: {AgentName}", selectedAgentName);

            return (responseText, selectedAgentName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in Agent Framework orchestration");
            return ("Sorry, I encountered an error while processing your request. Please try again.", "Error");
        }
    }

    private void LogDebugInfo(string prefix, object info)
    {
        foreach (var prop in info.GetType().GetProperties())
        {
            var value = prop.GetValue(info, null);
            LogMessage($"{prefix} - {prop.Name}", value?.ToString() ?? "null");
        }
    }

    //private async Task<AgentType> SelectNextAgent(string conversationContext, AgentType currentAgent)
    //{
    //    try
    //    {
    //        // Load selection strategy prompt
    //        var selectionPrompt = File.ReadAllText("Prompts/SelectionStrategy.prompty");

    //        var selectionMessages = new List<OpenAI.Chat.ChatMessage>
    //        {
    //            new SystemChatMessage(selectionPrompt),
    //            new UserChatMessage($"RESPONSE: {conversationContext}")
    //        };

    //        // Use structured output for agent selection
    //        var chatOptions = new ChatCompletionOptions
    //        {
    //            ResponseFormat = OpenAI.Chat.ChatResponseFormat.CreateJsonSchemaFormat(
    //                "agent_selection",
    //                BinaryData.FromString(ChatResponseFormatBuilder.BuildFormat(ChatResponseFormatBuilder.ChatResponseStrategy.Continuation)),
    //                "Select the next agent and provide reasoning"
    //            )
    //        };

    //        var result = await _chatClient.CompleteChatAsync(selectionMessages, chatOptions);
    //        var responseContent = result.Value.Content.FirstOrDefault()?.Text ?? "{}";

    //        // Parse the structured response
    //        var selectionInfo = JsonSerializer.Deserialize<AgentSelectionInfo>(responseContent);

    //        // Log the selection details
    //        LogMessage("SELECTION - Agent", selectionInfo?.AgentName ?? "Coordinator");
    //        LogMessage("SELECTION - Reason", selectionInfo?.Reason ?? "Default selection");

    //        // Parse agent name to AgentType
    //        return ParseAgentName(selectionInfo?.AgentName ?? "Coordinator");
    //    }
    //    catch (Exception ex)
    //    {
    //        _logger.LogError(ex, "Error in agent selection, defaulting to Coordinator");
    //        return AgentType.Coordinator;
    //    }
    //}

    //private async Task<bool> ShouldTerminateConversation(string conversationContext)
    //{
    //    try
    //    {
    //        // Load termination strategy prompt
    //        var terminationPrompt = File.ReadAllText("Prompts/TerminationStrategy.prompty");

    //        var terminationMessages = new List<OpenAI.Chat.ChatMessage>
    //        {
    //            new SystemChatMessage(terminationPrompt),
    //            new UserChatMessage($"RESPONSE: {conversationContext}")
    //        };

    //        // Use structured output for termination decision
    //        var chatOptions = new ChatCompletionOptions
    //        {
    //            ResponseFormat = OpenAI.Chat.ChatResponseFormat.CreateJsonSchemaFormat(
    //                "termination_decision",
    //                BinaryData.FromString(ChatResponseFormatBuilder.BuildFormat(ChatResponseFormatBuilder.ChatResponseStrategy.Termination)),
    //                "Determine if conversation should continue and provide reasoning"
    //            )
    //        };

    //        var result = await _chatClient.CompleteChatAsync(terminationMessages, chatOptions);
    //        var responseContent = result.Value.Content.FirstOrDefault()?.Text ?? "{}";

    //        // Parse the structured response
    //        var terminationInfo = JsonSerializer.Deserialize<TerminationInfo>(responseContent);

    //        // Log the termination details
    //        LogMessage("TERMINATION - Should Continue", terminationInfo?.ShouldContinue.ToString() ?? "true");
    //        LogMessage("TERMINATION - Reason", terminationInfo?.Reason ?? "Default continuation");

    //        // Return the inverse of ShouldContinue (if should continue = false, then terminate = true)
    //        return !(terminationInfo?.ShouldContinue ?? true);
    //    }
    //    catch (Exception ex)
    //    {
    //        _logger.LogError(ex, "Error in termination decision, defaulting to continue");
    //        return false;
    //    }
    //}

    //private string CreateConversationContext(List<OpenAI.Chat.ChatMessage> messages)
    //{
    //    var context = string.Join("\n", messages.TakeLast(5).Select(m => 
    //    {
    //        var role = m switch
    //        {
    //            UserChatMessage => "User",
    //            AssistantChatMessage => "Assistant",
    //            SystemChatMessage => "System",
    //            _ => "Unknown"
    //        };

    //        var content = m switch
    //        {
    //            UserChatMessage userMsg => userMsg.Content.FirstOrDefault()?.Text ?? "",
    //            AssistantChatMessage assistantMsg => assistantMsg.Content.FirstOrDefault()?.Text ?? "",
    //            SystemChatMessage systemMsg => systemMsg.Content.FirstOrDefault()?.Text ?? "",
    //            _ => ""
    //        };

    //        return $"{role}: {content}";
    //    }));

    //    return context;
    //}

    private AgentType ParseAgentName(string agentName)
    {
        return agentName.ToLowerInvariant() switch
        {
            "sales" => AgentType.Sales,
            "transactions" => AgentType.Transactions,
            "customersupport" => AgentType.CustomerSupport,
            "coordinator" => AgentType.Coordinator,
            _ => AgentType.Coordinator
        };
    }

    public async Task<string> Summarize(string sessionId, string userPrompt)
    {
        try
        {
            //    var messages = new List<OpenAI.Chat.ChatMessage>
            //    {
            //        new SystemChatMessage("Summarize the following text into exactly two words:"),
            //        new UserChatMessage(userPrompt)
            //    };

            //    var chatOptions = new ChatCompletionOptions();
            //    var response = await _chatClient.CompleteChatAsync(messages, chatOptions);

            //    return response.Value.Content.FirstOrDefault()?.Text ?? "No summary generated";

            return "TBD";
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
            _logger.LogInformation("Agent Framework service initialized.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Agent Framework service was not initialized. The following error occurred: {ErrorMessage}.", ex.ToString());
        }
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        // Dispose resources if any
    }
}