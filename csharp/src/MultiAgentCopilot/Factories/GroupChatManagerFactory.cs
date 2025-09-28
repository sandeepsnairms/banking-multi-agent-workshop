using Microsoft.Agents.Orchestration;
using Microsoft.Extensions.AI;
using Newtonsoft.Json.Linq;

namespace MultiAgentCopilot.MultiAgentCopilot.Factories
{
    public class GroupChatManagerFactory : GroupChatManager
    {

        private readonly IChatClient _chatClient;
        private readonly ILogger<GroupChatManagerFactory> _logger;
        private readonly string _topic;
        LogCallback _logCallback;


        public delegate void LogCallback(string key, string value);

        public GroupChatManagerFactory(string topic, IChatClient chatClient, LogCallback logCallback)
        {
            _chatClient = chatClient;
            //_logger = loggerFactory.CreateLogger<BankingGroupChatManager>();
            _topic = topic;
            _logCallback=logCallback;

            //_logger.LogInformation("Initialized BankingGroupChatManager with AI-based selection, filter, and termination strategies");
        }

        private async ValueTask<GroupChatManagerResult<TValue>> GetResponseAsync<TValue>(IReadOnlyCollection<Microsoft.Extensions.AI.ChatMessage> history, string prompt, CancellationToken cancellationToken = default)
        {
            ChatResponse<GroupChatManagerResult<TValue>> response = await _chatClient.GetResponseAsync<GroupChatManagerResult<TValue>>([.. history, new Microsoft.Extensions.AI.ChatMessage(ChatRole.System, prompt)], new ChatOptions { ToolMode = ChatToolMode.Auto }, useJsonSchemaResponseFormat: true, cancellationToken);
            return response.Result;
        }

        private static class Prompts
        {
            public static string Termination(string topic)
            {

                var terminationPrompt = $"{File.ReadAllText("Prompts/TerminationStrategy.prompty")}";
                return terminationPrompt.Replace("{topic}", topic);
            }


            public static string Selection(string topic, string participants)
            {
                var selectionPrompt = $"{File.ReadAllText("Prompts/SelectionStrategy.prompty")}";
                return selectionPrompt.Replace("{topic}", topic).Replace("{participants}", participants);
            }

            public static string Filter(string topic)
            {
                var filterPrompt = $"{File.ReadAllText("Prompts/FilterStrategy.prompty")}";
                return filterPrompt.Replace("{topic}", topic);

            }
               
        }


        protected async override ValueTask<GroupChatManagerResult<string>> FilterResultsAsync(IReadOnlyCollection<Microsoft.Extensions.AI.ChatMessage> history, CancellationToken cancellationToken = default)
        {
            var response = await this.GetResponseAsync<string>(history, Prompts.Filter(_topic), cancellationToken);
            // Now you can access Reason and Value from the awaited result
            var reason = response.Reason;
            var value = response.Value;

            _logCallback("FilterResultsAsync: Value", value);
            _logCallback("FilterResultsAsync: Reason", reason);

            return response;
        }

        protected override async ValueTask<GroupChatManagerResult<string>> SelectNextAgentAsync(IReadOnlyCollection<Microsoft.Extensions.AI.ChatMessage> history, GroupChatTeam team, CancellationToken cancellationToken = default)
        {
            var response = await this.GetResponseAsync<string>(history, Prompts.Selection(_topic, team.FormatList()), cancellationToken);

            // Now you can access Reason and Value from the awaited result
            var reason = response.Reason;
            var value = response.Value;
            
            _logCallback("SelectNextAgentAsync: Value", value);
            _logCallback("SelectNextAgentAsync: Reason", reason);

            return response;
        }

        protected override async ValueTask<GroupChatManagerResult<bool>> ShouldRequestUserInputAsync(IReadOnlyCollection<Microsoft.Extensions.AI.ChatMessage> history, CancellationToken cancellationToken = default)
        {
            var response = await this.GetResponseAsync<bool>(history, Prompts.Termination(_topic), cancellationToken);

            // Now you can access Reason and Value from the awaited result
            var reason = response.Reason;
            var value = response.Value;

            _logCallback("ShouldRequestUserInputAsync: Value", value.ToString());
            _logCallback("ShouldRequestUserInputAsync: Reason", reason);

            return response;
        }
    }
}
