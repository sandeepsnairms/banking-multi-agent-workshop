using Microsoft.Agents.AI;
using Microsoft.Agents.Orchestration;
using Microsoft.Extensions.AI;
using MultiAgentCopilot.Models.ChatInfoFormats;
using Newtonsoft.Json.Linq;
using OpenAI;
using System.Diagnostics.Metrics;
using System.Text.Json;

namespace MultiAgentCopilot.MultiAgentCopilot.Factories
{
    public class GroupChatManagerFactory : GroupChatManager
    {

        private readonly OpenAI.Chat.ChatClient _chatClient;
        private readonly ILogger<GroupChatManagerFactory> _logger;
        private readonly string _topic;
        LogCallback _logCallback;
        
        int _counter = 0;

        public delegate void LogCallback(string key, string value);

        public GroupChatManagerFactory(string topic, OpenAI.Chat.ChatClient chatClient, LogCallback logCallback)
        {
            _chatClient = chatClient;
            //_logger = loggerFactory.CreateLogger<BankingGroupChatManager>();
            _topic = topic;
            _logCallback=logCallback;


            //_logger.LogInformation("Initialized BankingGroupChatManager with AI-based selection, filter, and termination strategies");
        }

        //private async ValueTask<GroupChatManagerResult<TValue>> GetResponseAsync<TValue>(IReadOnlyCollection<Microsoft.Extensions.AI.ChatMessage> history, string prompt, CancellationToken cancellationToken = default)
        //{


        //    ChatResponse<GroupChatManagerResult<TValue>> response = await _chatClient.GetChatCompletionMessagesAsync<GroupChatManagerResult<TValue>>([.. history, new Microsoft.Extensions.AI.ChatMessage(ChatRole.System, prompt)], new ChatOptions { ToolMode = ChatToolMode.Auto }, useJsonSchemaResponseFormat: true, cancellationToken);
        //    return response.Result;
        //}

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
            //var response = await this.GetResponseAsync<string>(history, Prompts.Filter(_topic), cancellationToken);
            //// Now you can access Reason and Value from the awaited result
            //var reason = response.Reason;
            //var value = response.Value;

            //_logCallback("FilterResultsAsync: Value", value);
            //_logCallback("FilterResultsAsync: Reason", reason);

            //return response;

            return new GroupChatManagerResult<string>(history.Last().Text);
        }

        protected override async ValueTask<GroupChatManagerResult<string>> SelectNextAgentAsync(IReadOnlyCollection<Microsoft.Extensions.AI.ChatMessage> history, GroupChatTeam team, CancellationToken cancellationToken = default)
        {

            // Create the agent options, specifying the response format to use a JSON schema based on the PersonInfo class.
            ChatClientAgentOptions agentOptions = new(name: "Moderator", instructions: Prompts.Selection(_topic, team.FormatList()))
            {
                ChatOptions = new()
                {
                    ResponseFormat = ChatResponseFormat.ForJsonSchema(
                        schema: AIJsonUtilities.CreateJsonSchema(typeof(ContinuationInfo)),
                        schemaName: "ContinuationInfo",
                        schemaDescription: "Information about selecting next agent in a conversation.")
                }
            };

            var agent =  _chatClient.CreateAIAgent(agentOptions);

            var response=await agent.RunAsync(history);

            var selectionInfo = response.Deserialize<ContinuationInfo>(JsonSerializerOptions.Web);
            
            var value = selectionInfo.AgentName.ToString();
            var reason  = selectionInfo.Reason;
            
            _logCallback("SelectNextAgentAsync: Value", value);
            _logCallback("SelectNextAgentAsync: Reason", reason);

            return new GroupChatManagerResult<string>(value);
        }

        protected override async ValueTask<GroupChatManagerResult<bool>> ShouldRequestUserInputAsync(IReadOnlyCollection<Microsoft.Extensions.AI.ChatMessage> history, CancellationToken cancellationToken = default)
        {
            //var response = await this.GetResponseAsync<bool>(history, Prompts.Termination(_topic), cancellationToken);

            //// Now you can access Reason and Value from the awaited result
            //var reason = response.Reason;
            //var value = response.Value;

            //_logCallback("ShouldRequestUserInputAsync: Value", value.ToString());
            //_logCallback("ShouldRequestUserInputAsync: Reason", reason);

            //return response;

            _counter++;
            ChatClientAgentOptions agentOptions = new(name: "Moderator", instructions: Prompts.Termination(_topic))
            {
                ChatOptions = new()
                {
                    ResponseFormat = ChatResponseFormat.ForJsonSchema(
                        schema: AIJsonUtilities.CreateJsonSchema(typeof(TerminationInfo)),
                        schemaName: "TerminationInfo",
                        schemaDescription: "Information about deciding if user input is required.")
                }
            };

            var agent = _chatClient.CreateAIAgent(agentOptions);

            var response = await agent.RunAsync(history);

            var terminationInfo = response.Deserialize<TerminationInfo>(JsonSerializerOptions.Web);
            
            var value = terminationInfo.ShouldContinue;
            var reason = terminationInfo.Reason;

            _logCallback("ShouldRequestUserInputAsync: Value", value.ToString());
            _logCallback("ShouldRequestUserInputAsync: Reason", reason);

            return new GroupChatManagerResult<bool>(value);
        }


        protected override async ValueTask<GroupChatManagerResult<bool>> ShouldTerminateAsync(IReadOnlyCollection<Microsoft.Extensions.AI.ChatMessage> history, CancellationToken cancellationToken = default)
        {
            //var response = await this.GetResponseAsync<bool>(history, Prompts.Termination(_topic), cancellationToken);

            //// Now you can access Reason and Value from the awaited result
            //var reason = response.Reason;
            //var value = response.Value;

            //_logCallback("ShouldRequestUserInputAsync: Value", value.ToString());
            //_logCallback("ShouldRequestUserInputAsync: Reason", reason);

            //return response;

            if(_counter>5)
            {
                return new GroupChatManagerResult<bool>(true);
            }


                ChatClientAgentOptions agentOptions = new(name: "Moderator", instructions: Prompts.Termination(_topic))
            {
                ChatOptions = new()
                {
                    ResponseFormat = ChatResponseFormat.ForJsonSchema(
                        schema: AIJsonUtilities.CreateJsonSchema(typeof(TerminationInfo)),
                        schemaName: "TerminationInfo",
                        schemaDescription: "Information about deciding if user input is required.")
                }
            };

            var agent = _chatClient.CreateAIAgent(agentOptions);

            var response = await agent.RunAsync(history);

            var terminationInfo = response.Deserialize<TerminationInfo>(JsonSerializerOptions.Web);
            
            var value = !terminationInfo.ShouldContinue;
            var reason = terminationInfo.Reason;

            _logCallback("ShouldTerminateAsync: Value", value.ToString());
            _logCallback("ShouldTerminateAsync: Reason", reason);

            return new GroupChatManagerResult<bool>(value);
        }
    }
}
