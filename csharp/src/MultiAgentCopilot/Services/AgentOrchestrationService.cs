using Microsoft.Extensions.Options;
using MultiAgentCopilot.Helper;
using Microsoft.Extensions.AI;
using Azure.Identity;
using MultiAgentCopilot.Factories;
using MultiAgentCopilot.Models.Debug;
using MultiAgentCopilot.Models.Chat;
using MultiAgentCopilot.Models.Configuration;
using MultiAgentCopilot.Monitoring;
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
    readonly OrchestrationMonitor _orchestrationMonitor;

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
        _orchestrationMonitor = new OrchestrationMonitor(_loggerFactory.CreateLogger<OrchestrationMonitor>());

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

            // Build the multi-agent orchestration system with monitoring
            var agentOrchestration = agentFactory.BuildAgentOrchestration(_aoClient, _completionsDeploymentName, _loggerFactory, LogMessage, bankService, tenantId, userId, _orchestrationMonitor);

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

            _logger.LogInformation("Starting multi-agent conversation processing for user {UserId} in session {SessionId}", userId, userMessage.SessionId);

            // Process the conversation through the multi-agent system and get detailed information
            var (agentResponses, selectionInfo, terminationInfo) = await agentOrchestration.ProcessConversationAsync(conversationHistory, userMessage.SessionId);

            _logger.LogInformation("Multi-agent conversation completed with {ResponseCount} responses from agents: {AgentNames}", 
                agentResponses.Count, 
                string.Join(", ", agentResponses.Select(r => r.AgentName)));

            // Log orchestration summary
            _logger.LogInformation("Orchestration Summary - Selections: {SelectionCount}, Terminations: {TerminationCount}, Agent Flow: {AgentFlow}",
                selectionInfo.Count,
                terminationInfo.Count,
                string.Join(" -> ", agentResponses.Select(r => r.AgentName)));

            if (selectionInfo.Any())
            {
                _logger.LogDebug("Selection Details: {SelectionDetails}", 
                    string.Join(" | ", selectionInfo.Select((s, i) => $"{i + 1}. {s.AgentName}: {s.Reason}")));
            }

            if (terminationInfo.Any())
            {
                _logger.LogDebug("Termination Details: {TerminationDetails}", 
                    string.Join(" | ", terminationInfo.Select((t, i) => $"{i + 1}. Continue={t.ShouldContinue}: {t.Reason}")));
            }

            // Convert responses to Message objects with specific agent names and enhanced debug info
            for (int i = 0; i < agentResponses.Count; i++)
            {
                string messageId = Guid.NewGuid().ToString();
                string debugLogId = Guid.NewGuid().ToString();

                // Use the specific agent name that provided the response
                string authorName = agentResponses[i].AgentName;
                string responseRole = ChatRole.Assistant.ToString();
                string responseText = agentResponses[i].Response;

                _logger.LogTrace("Creating message from agent {AgentName} with content length {ContentLength}", 
                    authorName, responseText.Length);

                completionMessages.Add(new Message(
                    userMessage.TenantId, 
                    userMessage.UserId, 
                    userMessage.SessionId, 
                    authorName, 
                    responseRole, 
                    responseText, 
                    messageId, 
                    debugLogId
                ));

                // Create debug log with enhanced agent and orchestration information
                var completionMessagesLog = new DebugLog(
                    userMessage.TenantId, 
                    userMessage.UserId, 
                    userMessage.SessionId, 
                    messageId, 
                    debugLogId
                );

                // Start with basic debug properties
                var debugProperties = new List<LogProperty>(_promptDebugProperties)
                {
                    new LogProperty("AgentName", authorName),
                    new LogProperty("ResponseIndex", i.ToString()),
                    new LogProperty("ResponseLength", responseText.Length.ToString())
                };

                // Add Selection details if available for this response
                if (i < selectionInfo.Count)
                {
                    var selection = selectionInfo[i];
                    debugProperties.AddRange(new[]
                    {
                        new LogProperty("Selection_AgentName", selection.AgentName),
                        new LogProperty("Selection_Reason", selection.Reason),
                        new LogProperty("Selection_Timestamp", DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ")),
                        new LogProperty("Selection_Index", i.ToString())
                    });
                }

                // Add Termination details if available for this response
                if (i < terminationInfo.Count)
                {
                    var termination = terminationInfo[i];
                    debugProperties.AddRange(new[]
                    {
                        new LogProperty("Termination_ShouldContinue", termination.ShouldContinue.ToString()),
                        new LogProperty("Termination_Reason", termination.Reason),
                        new LogProperty("Termination_Timestamp", DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ")),
                        new LogProperty("Termination_Index", i.ToString())
                    });
                }

                // Add orchestration summary and agent flow information
                debugProperties.AddRange(new[]
                {
                    new LogProperty("TotalSelections", selectionInfo.Count.ToString()),
                    new LogProperty("TotalTerminationChecks", terminationInfo.Count.ToString()),
                    new LogProperty("ConversationCompleted", (i == agentResponses.Count - 1).ToString()),
                    new LogProperty("AgentSequence", string.Join(" -> ", agentResponses.Take(i + 1).Select(r => r.AgentName))),
                    new LogProperty("IsFirstResponse", (i == 0).ToString()),
                    new LogProperty("IsLastResponse", (i == agentResponses.Count - 1).ToString())
                });

                // Add selection summary for all agents chosen in this conversation
                if (selectionInfo.Any())
                {
                    debugProperties.Add(new LogProperty("AllSelectedAgents", 
                        string.Join(", ", selectionInfo.Select(s => s.AgentName).Distinct())));
                    debugProperties.Add(new LogProperty("SelectionReasons", 
                        string.Join(" | ", selectionInfo.Select((s, idx) => $"{idx + 1}: {s.Reason}"))));
                }

                // Add termination summary
                if (terminationInfo.Any())
                {
                    debugProperties.Add(new LogProperty("TerminationHistory", 
                        string.Join(" | ", terminationInfo.Select((t, idx) => $"{idx + 1}: Continue={t.ShouldContinue}"))));
                    debugProperties.Add(new LogProperty("FinalTerminationReason", 
                        terminationInfo.LastOrDefault()?.Reason ?? "Unknown"));
                }

                completionMessagesLog.PropertyBag = debugProperties;
                completionMessagesLogs.Add(completionMessagesLog);
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

    /// <summary>
    /// Get real-time session analytics for monitoring
    /// </summary>
    public SessionAnalytics? GetSessionAnalytics(string sessionId)
    {
        return _orchestrationMonitor.GetSessionAnalytics(sessionId);
    }

    /// <summary>
    /// Get overall orchestration statistics for monitoring
    /// </summary>
    public OrchestrationStatistics GetOrchestrationStatistics()
    {
        return _orchestrationMonitor.GetOverallStatistics();
    }
}