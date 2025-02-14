using MultiAgentCopilot.Common.Models.BusinessDomain;
using MultiAgentCopilot.ChatInfrastructure.Interfaces;
using Microsoft.AspNetCore.Mvc;
using MultiAgentCopilot.Common.Models.Debug;


namespace ChatAPI
{
    public class ChatEndpoints
    {
        private readonly IChatService _chatService;

        public ChatEndpoints(IChatService chatService)
        {
            _chatService = chatService;
        }

        public void Map(WebApplication app)
        {
            app.MapGet("/status", () => _chatService.Status)
                .WithName("GetServiceStatus");

            app.MapGet("/tenant/{tenantId}/user/{userId}/sessions/", async (string tenantId, string userId) =>
                await _chatService.GetAllChatSessionsAsync(tenantId, userId))
                .WithName("GetAllChatSessions");

            app.MapGet("/tenant/{tenantId}/user/{userId}/sessions/{sessionId}/messages",
                    async (string tenantId, string userId, string sessionId) =>
                    await _chatService.GetChatSessionMessagesAsync(tenantId, userId, sessionId))
                .WithName("GetChatSessionMessages");

            app.MapPost("/tenant/{tenantId}/user/{userId}/sessions/{sessionId}/message/{messageId}/rate",
                    async (string tenantId, string userId, string messageId, string sessionId, bool? rating) =>
                    await _chatService.RateMessageAsync(tenantId, userId, messageId, sessionId, rating))
                .WithName("RateMessage");

            app.MapGet("/tenant/{tenantId}/user/{userId}/sessions/{sessionId}/completiondetails/{debuglogId}",
                    async (string tenantId, string userId, string sessionId, string debuglogId) =>
                    await _chatService.GetChatCompletionDetailsAsync(tenantId, userId, sessionId, debuglogId))
                .WithName("GetChatCompletionDetails");

            app.MapPost("/tenant/{tenantId}/user/{userId}/sessions/", async (string tenantId, string userId) =>
                await _chatService.CreateNewChatSessionAsync())
                .WithName("CreateNewChatSession");

            app.MapPost("/tenant/{tenantId}/user/{userId}/sessions/{sessionId}/rename", async (string tenantId, string userId, string sessionId, string newChatSessionName) =>
                    await _chatService.RenameChatSessionAsync(tenantId, userId, sessionId, newChatSessionName))
                .WithName("RenameChatSession");

            app.MapDelete("/tenant/{tenantId}/user/{userId}/sessions/{sessionId}", async (string tenantId, string userId, string sessionId) =>
                    await _chatService.DeleteChatSessionAsync(tenantId, userId, sessionId))
                .WithName("DeleteChatSession");

            app.MapPost("/tenant/{tenantId}/user/{userId}/sessions/{sessionId}/completion", async (string tenantId, string userId, string sessionId, [FromBody] string userPrompt) =>
                    await _chatService.GetChatCompletionAsync(tenantId, userId, sessionId, userPrompt))
                .WithName("GetChatCompletionDetailsAsync");

            app.MapPost("/tenant/{tenantId}/user/{userId}/sessions/{sessionId}/summarize-name", async (string tenantId, string userId, string sessionId, [FromBody] string prompt) =>
                    await _chatService.SummarizeChatSessionNameAsync(tenantId, userId, sessionId, prompt))
                .WithName("SummarizeChatSessionName");

            app.MapPost("/tenant/{tenantId}/user/{userId}/semanticcache/reset", async (string tenantId, string userId) =>
                await _chatService.ResetSemanticCache(tenantId, userId))
                .WithName("ResetSemanticCache");
        }
    }
}
