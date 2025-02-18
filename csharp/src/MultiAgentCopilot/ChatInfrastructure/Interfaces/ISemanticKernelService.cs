using BankingAPI.Interfaces;
using MultiAgentCopilot.Common.Models.Chat;
using MultiAgentCopilot.Common.Models.Debug;

namespace MultiAgentCopilot.ChatInfrastructure.Interfaces
{
    public interface ISemanticKernelService
    {
        Task<Tuple<List<Message>, List<DebugLog>>> GetResponse(Message userMessage, List<Message> messageHistory, IBankDBService bankService, string tenantId, string userId);

        Task<string> Summarize(string sessionId, string userPrompt);

        Task ResetSemanticCache();
    }
}
