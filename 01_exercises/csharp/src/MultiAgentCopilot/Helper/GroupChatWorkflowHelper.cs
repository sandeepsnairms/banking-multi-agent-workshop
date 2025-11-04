using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using MultiAgentCopilot.Models.ChatInfoFormats;
using MultiAgentCopilot.MultiAgentCopilot.Factories;
using OpenAI;
using OpenAI.Chat;
using System.Text.Json;
using ChatMessage = Microsoft.Extensions.AI.ChatMessage;

namespace MultiAgentCopilot.MultiAgentCopilot.Helper
{
    public class GroupChatWorkflowHelper : GroupChatManager
    {
        private readonly IReadOnlyList<AIAgent> _agents;
        private readonly Func<GroupChatWorkflowHelper, IEnumerable<ChatMessage>, CancellationToken, ValueTask<bool>>? _shouldTerminateFunc;
        private readonly IChatClient _chatClient; // Add this field
        private int _nextIndex;
        private LogCallback _logCallback;

        public delegate void LogCallback(string key, string value);

        public GroupChatWorkflowHelper(
            IReadOnlyList<AIAgent> agents,
            IChatClient chatClient,
            LogCallback logCallback,
            Func<GroupChatWorkflowHelper, IEnumerable<ChatMessage>, CancellationToken, ValueTask<bool>>? shouldTerminateFunc = null)
        {
            _agents = agents;
            _chatClient = chatClient; // Store the chat client
            _logCallback = logCallback;
            _shouldTerminateFunc = shouldTerminateFunc;
        }

        private string GetAgentNames()
        {
            return string.Join(", ", _agents.Select(a => a.Name));
        }


        protected override async ValueTask<AIAgent> SelectNextAgentAsync(IReadOnlyList<ChatMessage> history, CancellationToken cancellationToken = default(CancellationToken))
        {
            return _agents[0];           
        }
      

        protected override async ValueTask<bool> ShouldTerminateAsync(IReadOnlyList<ChatMessage> history, CancellationToken cancellationToken = default(CancellationToken))
        {

            // Check if the last user message was from user, if so, do not terminate, skip system messages
            for(int i = history.Count -1; i >=0; i--)
            {
                if(history[i].Role == ChatRole.System)
                {
                    continue;
                }
                else if(history[i].Role == ChatRole.User)
                {
                    return false;
                }
                else
                {
                    break;
                }
            }       

            // First check if there's a custom termination function
            if (_shouldTerminateFunc != null)
            {
                bool customResult = await _shouldTerminateFunc(this, history, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
                if (customResult)
                {
                    return true;
                }
            }

            // Use AI-based termination decision using TerminationStrategy.prompty
            try
            {
                var shouldTerminate = await ShouldTerminateWithAI(history, cancellationToken);
                if (shouldTerminate)
                {
                    return true;
                }
            }
            catch (Exception ex)
            {
                _logCallback.Invoke("ShouldTerminateAsync Error", ex.Message);
            }

            // Fall back to base implementation
            return await base.ShouldTerminateAsync(history, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
        }

        private async Task<bool> ShouldTerminateWithAI(IReadOnlyList<ChatMessage> history, CancellationToken cancellationToken)
        {
            return false;
        }

        protected override void Reset()
        {
            base.Reset();
            _nextIndex = 0;
        }
    }
}
