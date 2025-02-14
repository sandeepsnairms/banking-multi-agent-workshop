using MultiAgentCopilot.Common.Interfaces;
using MultiAgentCopilot.Common.Models.BusinessDomain;
using MultiAgentCopilot.Common.Models.Chat;
using MultiAgentCopilot.ChatInfrastructure.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel.ChatCompletion;
using MultiAgentCopilot.Common.Models.Debug;
using System.Collections.Generic;

namespace MultiAgentCopilot.ChatInfrastructure.Services;

public class ChatService : IChatService
{
    private readonly ICosmosDBService _cosmosDBService;
    private readonly ISemanticKernelService _skService;
    private readonly ILogger _logger;

    public string Status
    {
        get
        {
            if (_cosmosDBService.IsInitialized && _skService.IsInitialized)
                return "ready";

            var status = new List<string>();

            if (!_cosmosDBService.IsInitialized)
                status.Add("CosmosDBService: initializing");
            if (!_skService.IsInitialized)
                status.Add("SemanticKernelService: initializing");

            return string.Join(",", status);
        }
    }

    public ChatService(
        ICosmosDBService cosmosDBService,
        ISemanticKernelService ragService,
        ILogger<ChatService> logger)
    {
        _cosmosDBService = cosmosDBService;
        _skService = ragService;
        _logger = logger;
    }

    /// <summary>
    /// Returns list of chat session ids and names.
    /// </summary>
    public async Task<List<Session>> GetAllChatSessionsAsync(string tenantId, string userId)
    {
        return await _cosmosDBService.GetSessionsAsync();
    }

    /// <summary>
    /// Returns the chat messages related to an existing session.
    /// </summary>
    public async Task<List<Message>> GetChatSessionMessagesAsync(string tenantId, string userId,string sessionId)
    {
        ArgumentNullException.ThrowIfNull(sessionId);
        return await _cosmosDBService.GetSessionMessagesAsync(sessionId);
    }

    /// <summary>
    /// Creates a new chat session.
    /// </summary>
    public async Task<Session> CreateNewChatSessionAsync()
    {
        Session session = new();
        return await _cosmosDBService.InsertSessionAsync(session);
    }

    /// <summary>
    /// Rename the chat session from its default (eg., "New Chat") to the summary provided by OpenAI.
    /// </summary>
    public async Task<Session> RenameChatSessionAsync(string tenantId, string userId,string sessionId, string newChatSessionName)
    {
        ArgumentNullException.ThrowIfNull(sessionId);
        ArgumentException.ThrowIfNullOrEmpty(newChatSessionName);

        return await _cosmosDBService.UpdateSessionNameAsync(sessionId, newChatSessionName);
    }

    /// <summary>
    /// Delete a chat session and related messages.
    /// </summary>
    public async Task DeleteChatSessionAsync(string tenantId, string userId,string sessionId)
    {
        ArgumentNullException.ThrowIfNull(sessionId);
        await _cosmosDBService.DeleteSessionAndMessagesAsync(sessionId);
    }

    /// <summary>
    /// Receive a prompt from a user, vectorize it from the OpenAI service, and get a completion from the OpenAI service.
    /// </summary>
    public async Task<List<Message>> GetChatCompletionAsync(string tenantId, string userId,string? sessionId, string userPrompt)
    {
        try
        {
            ArgumentNullException.ThrowIfNull(sessionId);

            // Retrieve conversation, including latest prompt.
            var archivedMessages = await _cosmosDBService.GetSessionMessagesAsync(sessionId);

            // Add both prompt and completion to cache, then persist in Cosmos DB
            var userMessage = new Message(sessionId, "User","User", userPrompt);

            // Generate the completion to return to the user
            var result = await _skService.GetResponse(userMessage, archivedMessages);

            await AddPromptCompletionMessagesAsync(tenantId, userId,sessionId, userMessage, result.Item1, result.Item2);

            return result.Item1;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error getting completion in session {sessionId} for user prompt [{userPrompt}].");
            return null;
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

            var summary = await _skService.Summarize(sessionId, prompt);

            var session = await RenameChatSessionAsync(tenantId, userId,sessionId, summary);

            return session.Name;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error getting a summary in session {sessionId} for user prompt [{prompt}].");
            return null;
        }

    }

    /// <summary>
    /// Add user prompt and AI assistance response to the chat session message list object and insert into the data service as a transaction.
    /// </summary>
    private async Task AddPromptCompletionMessagesAsync(string tenantId, string userId,string sessionId, Message promptMessage, List<Message> completionMessages, List<DebugLog> completionMessageLogs)
    {
        var session = await _cosmosDBService.GetSessionAsync(sessionId);

        completionMessages.Insert(0, promptMessage);
            await _cosmosDBService.UpsertSessionBatchAsync(completionMessages, completionMessageLogs, session);
    }

    /// <summary>
    /// Rate an assistant message. This can be used to discover useful AI responses for training, discoverability, and other benefits down the road.
    /// </summary>
    public async Task<Message> RateMessageAsync(string tenantId, string userId,string id, string sessionId, bool? rating)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(sessionId);

        return await _cosmosDBService.UpdateMessageRatingAsync(id, sessionId, rating);
    }

    public async Task<DebugLog> GetChatCompletionDetailsAsync(string tenantId, string userId,string sessionId, string completionPromptId)
    {
        ArgumentException.ThrowIfNullOrEmpty(sessionId);
        ArgumentException.ThrowIfNullOrEmpty(completionPromptId);

        return await _cosmosDBService.GetChatCompletionDetailsAsync(sessionId, completionPromptId);
    }

    public async Task ResetSemanticCache(string tenantId, string userId) =>
        await _skService.ResetSemanticCache();
}
