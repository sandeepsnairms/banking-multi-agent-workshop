using MultiAgentCopilot.Common.Models.BusinessDomain;
using MultiAgentCopilot.Common.Models.Chat;
using MultiAgentCopilot.Common.Models.Debug;
namespace MultiAgentCopilot.ChatInfrastructure.Interfaces;

public interface IChatService
{
    string Status { get; }

    /// <summary>
    /// Returns list of chat session ids and names for left-hand nav to bind to (display Name and ChatSessionId as hidden)
    /// </summary>
    Task<List<Session>> GetAllChatSessionsAsync(string tenantId, string userId);

    /// <summary>
    /// Returns the chat messages to display on the main web page when the user selects a chat from the left-hand nav
    /// </summary>
    Task<List<Message>> GetChatSessionMessagesAsync(string tenantId, string userId,string sessionId);

    /// <summary>
    /// User creates a new Chat Session.
    /// </summary>
    Task<Session> CreateNewChatSessionAsync();

    /// <summary>
    /// Rename the Chat Session from "New Chat" to the summary provided by OpenAI
    /// </summary>
    Task<Session> RenameChatSessionAsync(string tenantId, string userId,string sessionId, string newChatSessionName);

    /// <summary>
    /// User deletes a chat session
    /// </summary>
    Task DeleteChatSessionAsync(string tenantId, string userId,string sessionId);

    /// <summary>
    /// Receive a prompt from a user, Vectorize it from _openAIService Get a completion from _openAiService
    /// </summary>
    Task<List<Message>> GetChatCompletionAsync(string tenantId, string userId,string? sessionId, string userPrompt);

    Task<string> SummarizeChatSessionNameAsync(string tenantId, string userId,string? sessionId, string prompt);

    /// <summary>
    /// Rate an assistant message. This can be used to discover useful AI responses for training, discoverability, and other benefits down the road.
    /// </summary>
    Task<Message> RateMessageAsync(string tenantId, string userId,string id, string sessionId, bool? rating);

    Task<DebugLog> GetChatCompletionDetailsAsync(string tenantId, string userId,string sessionId, string completionPromptId);

    Task ResetSemanticCache(string tenantId, string userId);
}
