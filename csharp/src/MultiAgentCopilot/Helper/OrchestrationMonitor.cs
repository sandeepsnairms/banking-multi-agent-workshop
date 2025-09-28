using Microsoft.Extensions.AI;
using Microsoft.Extensions.AI.Agents;
using Microsoft.Extensions.AI.Agents;
using System.Text;
using System.Text.Json;
using OpenAIClient = OpenAI.OpenAIClient;

namespace MultiAgentCopilot.MultiAgentCopilot.Helper
{
    /// <summary>
    /// Provides monitoring and callback functionality for orchestration scenarios, including tracking streamed responses and message history.
    /// </summary>
    public class OrchestrationMonitor
    {
        /// <summary>
        /// Gets the list of streamed response updates received so far.
        /// </summary>
        public List<AgentRunResponseUpdate> StreamedResponses { get; } = [];

        /// <summary>
        /// Gets the list of chat messages representing the conversation history.
        /// </summary>
        public List<ChatMessage> History { get; } = [];

        /// <summary>
        /// Callback to handle a batch of chat messages, adding them to history and writing them to output.
        /// </summary>
        /// <param name="response">The collection of <see cref="ChatMessage"/> objects to process.</param>
        /// <returns>A <see cref="ValueTask"/> representing the asynchronous operation.</returns>
        public ValueTask ResponseCallbackAsync(IEnumerable<ChatMessage> response)
        {
            WriteStreamedResponse(StreamedResponses);
            StreamedResponses.Clear();

            History.AddRange(response);
            WriteResponse(response);
            return default;
        }

        private void WriteStreamedResponse(IEnumerable<AgentRunResponseUpdate> streamedResponses)
        {
            string? authorName = null;
            ChatRole? authorRole = null;
            StringBuilder builder = new();
            foreach (AgentRunResponseUpdate response in streamedResponses)
            {
                authorName ??= response.AuthorName;
                authorRole ??= response.Role;

                if (!string.IsNullOrEmpty(response.Text))
                {
                    builder.Append($"({JsonSerializer.Serialize(response.Text)})");
                }
            }

            if (builder.Length > 0)
            {
                Console.WriteLine($"\n# STREAMED {authorRole ?? ChatRole.Assistant}{(authorName is not null ? $" - {authorName}" : string.Empty)}: {builder}\n");
            }
        }

        private  void WriteResponse(IEnumerable<ChatMessage> response)
        {
            foreach (ChatMessage message in response)
            {
                if (!string.IsNullOrEmpty(message.Text))
                {
                    Console.WriteLine($"\n# RESPONSE {message.Role}{(message.AuthorName is not null ? $" - {message.AuthorName}" : string.Empty)}: {message}");
                }
            }
        }

        /// <summary>
        /// Callback to handle a streamed agent run response update, adding it to the list and writing output if final.
        /// </summary>
        /// <param name="streamedResponse">The <see cref="AgentRunResponseUpdate"/> to process.</param>
        /// <returns>A <see cref="ValueTask"/> representing the asynchronous operation.</returns>
        public ValueTask StreamingResultCallbackAsync(AgentRunResponseUpdate streamedResponse)
        {
            StreamedResponses.Add(streamedResponse);
            return default;
        }
    }
}
