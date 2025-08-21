using Microsoft.Extensions.Options;
using Microsoft.Extensions.AI;
using Azure.Identity;
using Azure.AI.OpenAI;
using OpenAI.Chat;
using MultiAgentCopilot.Models.Debug;
using MultiAgentCopilot.Models.Chat;
using MultiAgentCopilot.Models.Configuration;
using MultiAgentCopilot.Models;
using MultiAgentCopilot.Factories;
using MultiAgentCopilot.Services;
using MultiAgentCopilot.StructuredFormats;
using MultiAgentCopilot.Models.ChatInfoFormats;
using System.Text.Json;

namespace MultiAgentCopilot.Services;

public class AgentFrameworkService : IDisposable
{
    private readonly AgentFrameworkServiceSettings _settings;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<AgentFrameworkService> _logger;
    private readonly ChatClient _chatClient;
    private readonly AgentFactory _agentFactory;
    
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

        // Create Azure OpenAI chat client
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
        _chatClient = openAIClient.GetChatClient(_settings.AzureOpenAISettings.CompletionsDeployment);

        _agentFactory = new AgentFactory(_chatClient, _loggerFactory);

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
            var chatHistory = new List<OpenAI.Chat.ChatMessage>();
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
                    chatHistory.Add(new UserChatMessage(msg.Text));
                }
                else if (role == ChatMessageRole.Assistant)
                {
                    chatHistory.Add(new AssistantChatMessage(msg.Text));
                }
                else if (role == ChatMessageRole.System)
                {
                    chatHistory.Add(new SystemChatMessage(msg.Text));
                }
            }

            // Add user message
            chatHistory.Add(new UserChatMessage(userMessage.Text));

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
        List<OpenAI.Chat.ChatMessage> chatHistory, 
        BankingDataService bankService, 
        string tenantId, 
        string userId)
    {
        const int maxIterations = 10;
        var iteration = 0;
        var lastResponse = string.Empty;
        var selectedAgentType = AgentType.Coordinator; // Start with coordinator
        
        // Group chat conversation history
        var groupChatHistory = new List<OpenAI.Chat.ChatMessage>(chatHistory);
        
        while (iteration < maxIterations)
        {
            iteration++;
            
            // OPTIMIZATION: Pre-compute conversation contexts once to avoid duplicate processing
            // SelectNextAgent needs full conversation context for better agent selection
            var fullConversationContext = CreateConversationContext(groupChatHistory);
            
            // 1. Agent Selection Strategy - Use AI to select next agent with reasoning
            selectedAgentType = await SelectNextAgent(fullConversationContext, selectedAgentType);
            var selectedAgentName = _agentFactory.GetAgentName(selectedAgentType);
            
            LogMessage("SELECTION - Iteration", iteration.ToString());
            
            // 2. Execute the selected agent
            var agentResponse = await _agentFactory.InvokeAgentAsync(selectedAgentType, groupChatHistory, bankService, tenantId, userId);
            
            // Add agent response to group chat history
            groupChatHistory.Add(new AssistantChatMessage($"[{selectedAgentName}]: {agentResponse}"));
            lastResponse = agentResponse;
            
            // OPTIMIZATION: Only compute recent context for termination decision
            // ShouldTerminateConversation only needs recent messages (last 3) for decision making
            var recentConversationContext = CreateConversationContext(groupChatHistory.TakeLast(3).ToList());
            
            // 3. Termination Strategy - Use AI to determine if conversation should continue with reasoning
            var shouldTerminate = await ShouldTerminateConversation(recentConversationContext);
                        
            if (shouldTerminate)
            {
                return (agentResponse, selectedAgentName);
            }
        }
        
        // If we reach max iterations, return the last response
        _logger.LogWarning("Group chat orchestration reached maximum iterations ({MaxIterations})", maxIterations);
        return (lastResponse, _agentFactory.GetAgentName(selectedAgentType));
    }

    private async Task<AgentType> SelectNextAgent(string conversationContext, AgentType currentAgent)
    {
        try
        {
            // Load selection strategy prompt
            var selectionPrompt = File.ReadAllText("Prompts/SelectionStrategy.prompty");
            
            var selectionMessages = new List<OpenAI.Chat.ChatMessage>
            {
                new SystemChatMessage(selectionPrompt),
                new UserChatMessage($"RESPONSE: {conversationContext}")
            };

            // Use structured output for agent selection
            var chatOptions = new ChatCompletionOptions
            {
                ResponseFormat = OpenAI.Chat.ChatResponseFormat.CreateJsonSchemaFormat(
                    "agent_selection",
                    BinaryData.FromString(ChatResponseFormatBuilder.BuildFormat(ChatResponseFormatBuilder.ChatResponseStrategy.Continuation)),
                    "Select the next agent and provide reasoning"
                )
            };

            var result = await _chatClient.CompleteChatAsync(selectionMessages, chatOptions);
            var responseContent = result.Value.Content.FirstOrDefault()?.Text ?? "{}";
            
            // Parse the structured response
            var selectionInfo = JsonSerializer.Deserialize<AgentSelectionInfo>(responseContent);
            
            // Log the selection details
            LogMessage("SELECTION - Agent", selectionInfo?.AgentName ?? "Coordinator");
            LogMessage("SELECTION - Reason", selectionInfo?.Reason ?? "Default selection");
            
            // Parse agent name to AgentType
            return ParseAgentName(selectionInfo?.AgentName ?? "Coordinator");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in agent selection, defaulting to Coordinator");
            return AgentType.Coordinator;
        }
    }

    private async Task<bool> ShouldTerminateConversation(string conversationContext)
    {
        try
        {
            // Load termination strategy prompt
            var terminationPrompt = File.ReadAllText("Prompts/TerminationStrategy.prompty");
            
            var terminationMessages = new List<OpenAI.Chat.ChatMessage>
            {
                new SystemChatMessage(terminationPrompt),
                new UserChatMessage($"RESPONSE: {conversationContext}")
            };

            // Use structured output for termination decision
            var chatOptions = new ChatCompletionOptions
            {
                ResponseFormat = OpenAI.Chat.ChatResponseFormat.CreateJsonSchemaFormat(
                    "termination_decision",
                    BinaryData.FromString(ChatResponseFormatBuilder.BuildFormat(ChatResponseFormatBuilder.ChatResponseStrategy.Termination)),
                    "Determine if conversation should continue and provide reasoning"
                )
            };

            var result = await _chatClient.CompleteChatAsync(terminationMessages, chatOptions);
            var responseContent = result.Value.Content.FirstOrDefault()?.Text ?? "{}";
            
            // Parse the structured response
            var terminationInfo = JsonSerializer.Deserialize<TerminationInfo>(responseContent);
            
            // Log the termination details
            LogMessage("TERMINATION - Should Continue", terminationInfo?.ShouldContinue.ToString() ?? "true");
            LogMessage("TERMINATION - Reason", terminationInfo?.Reason ?? "Default continuation");
            
            // Return the inverse of ShouldContinue (if should continue = false, then terminate = true)
            return !(terminationInfo?.ShouldContinue ?? true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in termination decision, defaulting to continue");
            return false;
        }
    }

    private string CreateConversationContext(List<OpenAI.Chat.ChatMessage> messages)
    {
        var context = string.Join("\n", messages.TakeLast(5).Select(m => 
        {
            var role = m switch
            {
                UserChatMessage => "User",
                AssistantChatMessage => "Assistant",
                SystemChatMessage => "System",
                _ => "Unknown"
            };
            
            var content = m switch
            {
                UserChatMessage userMsg => userMsg.Content.FirstOrDefault()?.Text ?? "",
                AssistantChatMessage assistantMsg => assistantMsg.Content.FirstOrDefault()?.Text ?? "",
                SystemChatMessage systemMsg => systemMsg.Content.FirstOrDefault()?.Text ?? "",
                _ => ""
            };
            
            return $"{role}: {content}";
        }));
        
        return context;
    }

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
            var messages = new List<OpenAI.Chat.ChatMessage>
            {
                new SystemChatMessage("Summarize the following text into exactly two words:"),
                new UserChatMessage(userPrompt)
            };

            var chatOptions = new ChatCompletionOptions();
            var response = await _chatClient.CompleteChatAsync(messages, chatOptions);
            
            return response.Value.Content.FirstOrDefault()?.Text ?? "No summary generated";
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