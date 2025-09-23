using Microsoft.Agents.Orchestration;
using Microsoft.Extensions.AI;

namespace MultiAgentCopilot.MultiAgentCopilot.Factories
{
    public class BankingGroupChatManager : GroupChatManager
    {

        private readonly IChatClient _chatClient;
        private readonly ILogger<BankingGroupChatManager> _logger;
        private readonly string _topic;




        public BankingGroupChatManager(string topic, IChatClient chatClient)
        {
            _chatClient = chatClient;
            //_logger = loggerFactory.CreateLogger<BankingGroupChatManager>();
            _topic = topic;


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


        protected override ValueTask<GroupChatManagerResult<string>> FilterResultsAsync(IReadOnlyCollection<Microsoft.Extensions.AI.ChatMessage> history, CancellationToken cancellationToken = default)
        {
            return this.GetResponseAsync<string>(history, Prompts.Filter(_topic), cancellationToken);
            //throw new NotImplementedException();
        }

        protected override ValueTask<GroupChatManagerResult<string>> SelectNextAgentAsync(IReadOnlyCollection<Microsoft.Extensions.AI.ChatMessage> history, GroupChatTeam team, CancellationToken cancellationToken = default)
        {
            return this.GetResponseAsync<string>(history, Prompts.Selection(_topic, team.FormatList()), cancellationToken);
            //throw new NotImplementedException();
        }

        protected override ValueTask<GroupChatManagerResult<bool>> ShouldRequestUserInputAsync(IReadOnlyCollection<Microsoft.Extensions.AI.ChatMessage> history, CancellationToken cancellationToken = default)
        {
            return new(new GroupChatManagerResult<bool>(false) { Reason = "The AI group chat manager does not request user input." });
        }
    }
}
