using Microsoft.Extensions.Options;
using MultiAgentCopilot.Helper;
using Microsoft.Extensions.AI;
using Azure.Identity;
using MultiAgentCopilot.Factories;
using MultiAgentCopilot.Models.Debug;
using MultiAgentCopilot.Models.Chat;
using MultiAgentCopilot.Models.Configuration;
using Azure.AI.OpenAI;
using System.Text;
using MultiAgentCopilot.Models;
using AgentFactory = MultiAgentCopilot.Factories.AgentFactory;

namespace MultiAgentCopilot.Services;

public class AgentOrchestrationService : IDisposable
{
    readonly SemanticKernelServiceSettings _settings;
    readonly ILoggerFactory _loggerFactory;
    readonly ILogger<AgentOrchestrationService> _logger;
    readonly AzureOpenAIClient _aoClient;
    readonly string _completionsDeploymentName;

    bool _serviceInitialized = false;
    List<LogProperty> _promptDebugProperties;

    public bool IsInitialized => _serviceInitialized;

    public AgentOrchestrationService(
        IOptions<SemanticKernelServiceSettings> options,
        ILoggerFactory loggerFactory)
    {
        _settings = options.Value;
        _loggerFactory = loggerFactory;
        _logger = _loggerFactory.CreateLogger<AgentOrchestrationService>();
        _promptDebugProperties = new List<LogProperty>();

        _logger.LogInformation("Initializing the Agent Orchestration service...");

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

        _aoClient = new AzureOpenAIClient(new Uri(_settings.AzureOpenAISettings.Endpoint), credential);
        _completionsDeploymentName = _settings.AzureOpenAISettings.CompletionsDeployment;

        Task.Run(Initialize).ConfigureAwait(false);
    }

    private void LogMessage(string key, string value)
    {
        _promptDebugProperties.Add(new LogProperty(key, value));
    }

    public async Task<Tuple<List<Message>, List<DebugLog>>> GetResponse(Message userMessage, List<Message> messageHistory, BankingDataService bankService, string tenantId, string userId)
    {
        try
        {
            AgentFactory agentFactory = new AgentFactory();

            // Build the multi-agent orchestration system
            var agentOrchestration = agentFactory.BuildAgentGroupChat(_aoClient, _completionsDeploymentName, _loggerFactory, LogMessage, bankService, tenantId, userId);

            _promptDebugProperties = new List<LogProperty>();

            List<Message> completionMessages = new();
            List<DebugLog> completionMessagesLogs = new();

            // Build conversation history using Microsoft.Extensions.AI.ChatMessage
            var conversationHistory = new List<Microsoft.Extensions.AI.ChatMessage>();
            
            // Add message history
            foreach (var chatMessage in messageHistory)
            {
                ChatRole chatRole = chatMessage.SenderRole?.ToLower() switch
                {
                    "system" => ChatRole.System,
                    "assistant" => ChatRole.Assistant,
                    _ => ChatRole.User
                };

                conversationHistory.Add(new Microsoft.Extensions.AI.ChatMessage(chatRole, chatMessage.Text));
            }

            // Add current user message
            conversationHistory.Add(new Microsoft.Extensions.AI.ChatMessage(ChatRole.User, userMessage.Text));

            // Process the conversation through the multi-agent system
            var responses = await agentOrchestration.ProcessConversationAsync(conversationHistory);

            // Convert responses to Message objects
            for (int i = 0; i < responses.Count; i++)
            {
                string messageId = Guid.NewGuid().ToString();
                string debugLogId = Guid.NewGuid().ToString();

                // For multi-agent responses, we'll use "MultiAgent" as the author name
                // In a more sophisticated implementation, you could track which agent provided each response
                string authorName = "MultiAgent";
                string responseRole = ChatRole.Assistant.ToString();

                completionMessages.Add(new Message(
                    userMessage.TenantId, 
                    userMessage.UserId, 
                    userMessage.SessionId, 
                    authorName, 
                    responseRole, 
                    responses[i], 
                    messageId, 
                    debugLogId
                ));

                // Create debug log if we have debug properties
                if (_promptDebugProperties.Count > 0)
                {
                    var completionMessagesLog = new DebugLog(
                        userMessage.TenantId, 
                        userMessage.UserId, 
                        userMessage.SessionId, 
                        messageId, 
                        debugLogId
                    );
                    completionMessagesLog.PropertyBag = new List<LogProperty>(_promptDebugProperties);
                    completionMessagesLogs.Add(completionMessagesLog);
                }
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
            var chatClient = _aoClient.GetChatClient(_completionsDeploymentName);
            
            var messages = new List<OpenAI.Chat.ChatMessage>
            {
                OpenAI.Chat.ChatMessage.CreateSystemMessage("Summarize the following text into exactly two words:"),
                OpenAI.Chat.ChatMessage.CreateUserMessage(userPrompt)
            };

            // Create options without setting MaxTokens since it's not available
            var options = new OpenAI.Chat.ChatCompletionOptions();
            
            var response = await chatClient.CompleteChatAsync(messages, options);

            var responseText = response.Value.Content[0].Text ?? "No summary generated";
            
            // Manually limit to approximately 2 words by taking first few words
            var words = responseText.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return words.Length > 2 ? string.Join(" ", words.Take(2)) : responseText;
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
            _logger.LogInformation("Agent Orchestration service initialized.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Agent Orchestration service was not initialized. The following error occurred: {ErrorMessage}.", ex.ToString());
        }
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        // Dispose resources if any
    }
}