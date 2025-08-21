using Microsoft.Extensions.Options;
using MultiAgentCopilot.Models.Debug;
using MultiAgentCopilot.Models.Configuration;
using MultiAgentCopilot.Models.Chat;
using Newtonsoft.Json.Linq;
using System.Text.Json;
using Newtonsoft.Json;


namespace MultiAgentCopilot.Services;

public class ChatService 
{
    private readonly CosmosDBService _cosmosDBService;
    private readonly BankingDataService _bankService;
    private readonly AgentFrameworkService _agentService;
    private readonly ILogger _logger;

    
    public ChatService(
        IOptions<CosmosDBSettings> cosmosOptions,
        IOptions<AgentFrameworkServiceSettings> skOptions,
        CosmosDBService cosmosDBService,
        AgentFrameworkService agentService,
        ILoggerFactory loggerFactory)
    {
        _cosmosDBService = cosmosDBService;
        _agentService = agentService;
        _bankService = new BankingDataService(cosmosDBService.Database,cosmosDBService.AccountDataContainer,cosmosDBService.UserDataContainer,cosmosDBService.AccountDataContainer,cosmosDBService.OfferDataContainer, skOptions.Value, loggerFactory);
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
    /// Receive a prompt from a user, and get a completion from the Agent Framework service.
    /// </summary>
    public async Task<List<Message>> GetChatCompletionAsync(string tenantId, string userId,string? sessionId, string userPrompt)
    {
        try
        {
            ArgumentNullException.ThrowIfNull(sessionId);

            // Get message history for the session
            var messageHistory = await GetChatSessionMessagesAsync(tenantId, userId, sessionId);

            // Create user message
            var userMessage = new Message(tenantId, userId, sessionId, "User", "User", userPrompt);

            // Get response from agent framework
            var result = await _agentService.GetResponse(userMessage, messageHistory, _bankService, tenantId, userId);
            
            var completionMessages = result.Item1;
            var debugLogs = result.Item2;

            // Save user message
            await _cosmosDBService.InsertMessageAsync(userMessage);
            
            // Save completion messages and debug logs
            foreach (var message in completionMessages)
            {
                await _cosmosDBService.InsertMessageAsync(message);
            }

            foreach (var debugLog in debugLogs)
            {
                await _cosmosDBService.InsertDebugLogAsync(debugLog);
            }

            // Return all messages (user + completions)
            var allMessages = new List<Message> { userMessage };
            allMessages.AddRange(completionMessages);
            
            return allMessages;
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
    public async Task<string> SummarizeChatSessionNameAsync(string tenantId, string userId,string? sessionId, string prompt)
    {
        try
        {
            ArgumentNullException.ThrowIfNull(sessionId);

            var summary = await _agentService.Summarize(sessionId, prompt);

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
            if (!docJObject!.ContainsKey("id"))
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

