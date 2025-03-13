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


namespace MultiAgentCopilot.ChatInfrastructure.Services;

public class ChatService : IChatService
{
    private readonly ICosmosDBService _cosmosDBService;
    private readonly ILogger _logger;

    
    public ChatService(
        IOptions<CosmosDBSettings> cosmosOptions,
        ICosmosDBService cosmosDBService,
        ILoggerFactory loggerFactory)
    {
        _cosmosDBService = cosmosDBService;    
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
    public async Task<List<Message>> GetChatCompletionAsync(string tenantId, string userId,string? sessionId, string userPrompt)
    {
        try
        {
            ArgumentNullException.ThrowIfNull(sessionId);
            

            // Add both prompt and completion to cache, then persist in Cosmos DB
            var userMessage = new Message(tenantId,userId,sessionId, "User","User", "## Replay user message ## " + userPrompt);
                      
            return new List<Message> { userMessage };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error getting completion in session {sessionId} for user prompt [{userPrompt}].");
#pragma warning disable CS8603 // Possible null reference return.
            return null;
#pragma warning restore CS8603 // Possible null reference return.
        }
    }

    /// <summary>
    /// Generate a name for a chat message, based on the passed in prompt.
    /// </summary>
    public async Task<string> SummarizeChatSessionNameAsync(string tenantId, string userId,string? sessionId, string prompt)
    {
        return "Not implemented";
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

