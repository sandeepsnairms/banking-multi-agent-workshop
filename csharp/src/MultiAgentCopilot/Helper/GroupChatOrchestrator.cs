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
   

    public class GroupChatOrchestrator:AgentWorkflowBuilder.GroupChatManager
    {

        private readonly IReadOnlyList<AIAgent> _agents;
        private readonly Func<GroupChatOrchestrator, IEnumerable<ChatMessage>, CancellationToken, ValueTask<bool>>? _shouldTerminateFunc;
        private readonly IChatClient _chatClient; // Add this field
        private int _nextIndex;
        private LogCallback _logCallback;


        public delegate void LogCallback(string key, string value);


        public GroupChatOrchestrator(
            IReadOnlyList<AIAgent> agents,
            IChatClient chatClient,
            LogCallback logCallback,
            Func<GroupChatOrchestrator, IEnumerable<ChatMessage>, CancellationToken, ValueTask<bool>>? shouldTerminateFunc = null)
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
            // Convert chat history to a string representation for the prompt
            var historyText = string.Join("\n", history.TakeLast(5).Select(msg => 
            {
                var role = msg.Role.ToString();
                var content = msg.Text ?? "";
                return $"{role}: {content}";
            }));
            
            // Create a moderator agent to decide which agent should respond next
            ChatClientAgentOptions agentOptions = new(name: "Moderator", instructions: PromptFactory.Selection(historyText, GetAgentNames()))
            {
                ChatOptions = new()
                {
                    ResponseFormat = Microsoft.Extensions.AI.ChatResponseFormat.ForJsonSchema(
                        schema: AIJsonUtilities.CreateJsonSchema(typeof(ContinuationInfo)),
                        schemaName: "ContinuationInfo",
                        schemaDescription: "Information about selecting next agent in a conversation.")
                }
            };

            var moderatorAgent = _chatClient.CreateAIAgent(agentOptions);

            // Get the selection recommendation from the moderator
            var response = await moderatorAgent.RunAsync(history);
            var selectionInfo = response.Deserialize<ContinuationInfo>(JsonSerializerOptions.Web);

            var selectedAgentName = selectionInfo?.AgentName?.ToString();
            var reason = selectionInfo?.Reason;

            // Log the selection decision (uncomment if you have logging)
            _logCallback?.Invoke("SelectNextAgentAsync", $"{{Agent: {selectedAgentName}, Reason: {reason}}}");

            // Find the matching agent from your agents list
            var selectedAgent = _agents.FirstOrDefault(agent =>
                string.Equals(agent.Name, selectedAgentName, StringComparison.OrdinalIgnoreCase));

            // Return the selected agent, or default to the first agent if no match found
            return selectedAgent ?? _agents[0];
        }

        protected override async ValueTask<bool> ShouldTerminateAsync(IReadOnlyList<ChatMessage> history, CancellationToken cancellationToken = default(CancellationToken))
        {
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
                    //_logCallback?.Invoke("ShouldTerminateAsync", "AI decided to terminate the conversation");
                    return true;
                }
            }
            catch (Exception ex)
            {
                //_logCallback?.Invoke("ShouldTerminateAsync Error", ex.Message);
                // Fall back to base implementation if AI termination fails
            }

            // Fall back to base implementation
            return await base.ShouldTerminateAsync(history, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
        }

        private async Task<bool> ShouldTerminateWithAI(IReadOnlyList<ChatMessage> history, CancellationToken cancellationToken)
        {
            if (history == null || !history.Any())
                return false;

            // Convert chat history to a string representation for the prompt
            var historyText = string.Join("\n", history.TakeLast(10).Select(msg =>
            {
                var role = msg.Role.ToString();
                var content = msg.Text ?? "";
                return $"{role}: {content}";
            }));

            // Create a termination decision agent using the TerminationStrategy.prompty
            ChatClientAgentOptions agentOptions = new(
                name: "TerminationDecider",
                instructions: PromptFactory.Termination(historyText))
            {
                ChatOptions = new()
                {
                    ResponseFormat = Microsoft.Extensions.AI.ChatResponseFormat.ForJsonSchema(
                        schema: AIJsonUtilities.CreateJsonSchema(typeof(TerminationInfo)),
                        schemaName: "TerminationInfo",
                        schemaDescription: "Information about whether the conversation should continue or terminate.")
                }
            };

            var terminationAgent = _chatClient.CreateAIAgent(agentOptions);

            try
            {
                // Get the termination decision from the AI agent
                var response = await terminationAgent.RunAsync(history);
                var terminationInfo = response.Deserialize<TerminationInfo>(JsonSerializerOptions.Web);

                var shouldContinue = terminationInfo?.ShouldContinue ?? true;
                var reason = terminationInfo?.Reason ?? "No reason provided";

                // Log the termination decision
                _logCallback?.Invoke("ShouldTerminateAsync", $"{{Continue: {shouldContinue.ToString()}, Reason: {reason}}}");

                // Return true if we should terminate (i.e., should NOT continue)
                return !shouldContinue;
            }
            catch (Exception ex)
            {
                _logCallback?.Invoke("ShouldTerminateWithAI Error", ex.Message);
                // Default to continue if there's an error
                return false;
            }
        }

        protected override void Reset()
        {
            base.Reset();
            _nextIndex = 0;
        }
    }

}
