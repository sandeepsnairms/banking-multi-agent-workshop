using MultiAgentCopilot.Common.Interfaces;
using MultiAgentCopilot.Common.Models.Chat;
using MultiAgentCopilot.Common.Models.Debug;

namespace MultiAgentCopilot.ChatInfrastructure.Interfaces
{
    public interface ISemanticKernelService
    {
        bool IsInitialized { get; }

        Task<Tuple<List<Message>, List<DebugLog>>> GetResponse(Message userMessage, List<Message> messageHistory);

        Task<string> Summarize(string sessionId, string userPrompt);

        Task ResetSemanticCache();
    }
}
