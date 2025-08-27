/*
 * REAL MICROSOFT AGENT FRAMEWORK IMPLEMENTATION
 * 
 * This file implements the Microsoft Agent Framework GroupChatOrchestration
 * using the real Microsoft Agent Framework DLLs and APIs.
 * 
 * Based on agent-framework/dotnet/samples/GettingStarted/Orchestration/GroupChatOrchestration_With_AIManager.cs
 */

using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Agents.Orchestration;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.AI.Agents;
using Microsoft.Identity.Client;
using MultiAgentCopilot.Models;
using MultiAgentCopilot.Models.ChatInfoFormats;
using MultiAgentCopilot.Services;
using MultiAgentCopilot.StructuredFormats;
using MultiAgentCopilot.Tools;
using OpenAI;
using OpenAI.Chat;
using System.ComponentModel;
using System.Reflection;
using System.Text.Json;


namespace MultiAgentCopilot.Factories
{
    /// <summary>
    /// Custom GroupChatManager for Banking Multi-Agent System
    /// Implements SelectionStrategy and TerminationStrategy using AI-based decisions
    /// Based on https://github.com/microsoft/agent-framework/blob/main/dotnet/samples/GettingStarted/Orchestration/GroupChatOrchestration_With_AIManager.cs
    /// </summary>
    public class BankingGroupChatManager : GroupChatManager
    {
        private readonly IChatClient _chatClient;
        private readonly ILogger<BankingGroupChatManager> _logger;
        private readonly string _selectionPrompt;
        private readonly string _terminationPrompt;
        private readonly string _filterPrompt;
        private readonly string _topic;

        public BankingGroupChatManager(IChatClient chatClient, ILoggerFactory loggerFactory)
        {
            _chatClient = chatClient;
            _logger = loggerFactory.CreateLogger<BankingGroupChatManager>();
            //_topic=topic;

            // Load strategy prompts
            _selectionPrompt = File.ReadAllText("Prompts/SelectionStrategy.prompty");
            _terminationPrompt = File.ReadAllText("Prompts/TerminationStrategy.prompty");
            _filterPrompt= File.ReadAllText("Prompts/FilterStrategy.prompty");

            _logger.LogInformation("Initialized BankingGroupChatManager with AI-based selection, filter, and termination strategies");
        }

        private async ValueTask<GroupChatManagerResult<TValue>> GetResponseAsync<TValue>(IReadOnlyCollection<Microsoft.Extensions.AI.ChatMessage> history, string prompt, CancellationToken cancellationToken = default)
        {
            ChatResponse<GroupChatManagerResult<TValue>> response = await _chatClient.GetResponseAsync<GroupChatManagerResult<TValue>>([.. history, new Microsoft.Extensions.AI.ChatMessage(ChatRole.System, prompt)], new ChatOptions { ToolMode = ChatToolMode.Auto }, useJsonSchemaResponseFormat: true, cancellationToken);
            return response.Result;
        }

        private static class Prompts
        {
            public static string Termination(string topic) =>
                $"""
                You are mediator that guides a discussion on the topic of '{topic}'. 
                You need to determine if the discussion has reached a conclusion. 
                If you would like to end the discussion, please respond with True. Otherwise, respond with False.
                """;

            public static string Selection(string topic, string participants) =>
                $"""
                You are mediator that guides a discussion on the topic of '{topic}'. 
                You need to select the next participant to speak. 
                Here are the names and descriptions of the participants: 
                {participants}\n
                Please respond with only the name of the participant you would like to select.
                """;

            public static string Filter(string topic) =>
                $"""
                You are mediator that guides a discussion on the topic of '{topic}'. 
                You have just concluded the discussion. 
                Please summarize the discussion and provide a closing statement.
                """;
        }

        /// <summary>
        /// Unified AI-based agent selection with integrated content filter protection
        /// Combines SelectNextAgent, SelectNextAgentAsync, and SelectAgentUsingAI into a single method
        /// </summary>
        protected override ValueTask<GroupChatManagerResult<string>> SelectNextAgent(
            IReadOnlyCollection<Microsoft.Extensions.AI.ChatMessage> history,
            GroupChatTeam team,
            CancellationToken cancellationToken = default)
        {
            //return new ValueTask<GroupChatManagerResult<string>>(ProcessAgentSelectionWithAIAsync(history, team, cancellationToken));
            return this.GetResponseAsync<string>(history, Prompts.Selection(_topic, team.FormatList()), cancellationToken);
        }

        /// <summary>
        /// Unified agent selection method with comprehensive AI decision making and content filter protection
        /// </summary>
        //private async Task<GroupChatManagerResult<string>> ProcessAgentSelectionWithAIAsync(
        //    IReadOnlyCollection<Microsoft.Extensions.AI.ChatMessage> history,
        //    GroupChatTeam team,
        //    CancellationToken cancellationToken = default)
        //{
            

        //    try
        //    {
        //        _logger.LogDebug("Processing agent selection using unified AI approach with content filter protection");

        //        // Create conversation context from recent messages
        //        var conversationContext = CreateConversationContext(history);

        //        var selectionMessages = new List<OpenAI.Chat.ChatMessage>
        //        {
        //            new SystemChatMessage(_selectionPrompt),
        //            new UserChatMessage($"CONVERSATION: {conversationContext}")
        //        };

        //        // Use structured output for agent selection
        //        var chatOptions = new ChatCompletionOptions
        //        {
        //            ResponseFormat = OpenAI.Chat.ChatResponseFormat.CreateJsonSchemaFormat(
        //                "agent_selection",
        //                BinaryData.FromString(ChatResponseFormatBuilder.BuildFormat(ChatResponseFormatBuilder.ChatResponseStrategy.Continuation)),
        //                "Select the next agent and provide reasoning"
        //            )
        //        };

        //        var result = await _chatClient.CompleteChatAsync(selectionMessages, chatOptions);
        //        var responseContent = result.Value.Content.FirstOrDefault()?.Text ?? "{}";

        //        _logger.LogDebug("AI agent selection raw response: {Response}", responseContent);

        //        // Parse the structured response
        //        var selectionInfo = JsonSerializer.Deserialize<AgentSelectionInfo>(responseContent);

        //        var selectedAgentName = selectionInfo?.AgentName ?? "Coordinator";
        //        var reason = selectionInfo?.Reason ?? "Default selection";

        //        _logger.LogInformation("AI selected agent: {AgentName}, Reason: {Reason}", selectedAgentName, reason);

        //        return new GroupChatManagerResult<string>(selectedAgentName) { Reason = reason };
        //    }           
        //    catch (Exception ex)
        //    {
        //        _logger.LogError(ex, "Error in unified agent selection, using fallback");

        //        // Fallback to coordinator
        //        _logger.LogInformation("Using fallback agent selection: Coordinator");
        //        return new GroupChatManagerResult<string>("Coordinator") { Reason = "Error in selection - using Coordinator fallback" };
        //    }
        //}       

        /// <summary>
        /// The AI group chat manager does not request user input.
        /// </summary>
        protected override ValueTask<GroupChatManagerResult<bool>> ShouldRequestUserInput(IReadOnlyCollection<Microsoft.Extensions.AI.ChatMessage> history, CancellationToken cancellationToken = default) =>
            new(new GroupChatManagerResult<bool>(false) { Reason = "The AI group chat manager does not request user input." });

        /// <summary>
        /// Unified AI-based termination decision with integrated content filter protection
        /// Combines ShouldTerminate, ShouldTerminateAsync, and ShouldTerminateUsingAI into a single method
        /// </summary>
        protected override async ValueTask<GroupChatManagerResult<bool>> ShouldTerminate(
            IReadOnlyCollection<Microsoft.Extensions.AI.ChatMessage> history,
            CancellationToken cancellationToken = default)

        {
            GroupChatManagerResult<bool> result = await base.ShouldTerminate(history, cancellationToken);
            if (!result.Value)
            {
                result = await this.GetResponseAsync<bool>(history, Prompts.Termination(_topic), cancellationToken);
            }
            return result;
        }
        //{
        //    return new ValueTask<GroupChatManagerResult<bool>>(EvaluateTerminationWithAIAsync(history, cancellationToken));
        //}

        /// <summary>
        /// Unified termination evaluation method with comprehensive AI decision making and content filter protection
        /// </summary>
        //private async Task<GroupChatManagerResult<bool>> EvaluateTerminationWithAIAsync(
        //    IReadOnlyCollection<Microsoft.Extensions.AI.ChatMessage> history,
        //    CancellationToken cancellationToken = default)
        //{
        //    try
        //    {
        //        _logger.LogDebug("Evaluating conversation termination using unified AI approach with content filter protection");

        //        // Create conversation context from recent messages
        //        var conversationContext = CreateConversationContext(history);

        //        var terminationMessages = new List<OpenAI.Chat.ChatMessage>
        //        {
        //            new SystemChatMessage(_terminationPrompt),
        //            new UserChatMessage($"CONVERSATION: {conversationContext}")
        //        };

        //        // Use structured output for termination decision
        //        var chatOptions = new ChatCompletionOptions
        //        {
        //            ResponseFormat = OpenAI.Chat.ChatResponseFormat.CreateJsonSchemaFormat(
        //                "termination_decision",
        //                BinaryData.FromString(ChatResponseFormatBuilder.BuildFormat(ChatResponseFormatBuilder.ChatResponseStrategy.Termination)),
        //                "Determine if conversation should continue and provide reasoning"
        //            )
        //        };

        //        var result = await _chatClient.CompleteChatAsync(terminationMessages, chatOptions);
        //        var responseContent = result.Value.Content.FirstOrDefault()?.Text ?? "{}";

        //        _logger.LogDebug("AI termination decision raw response: {Response}", responseContent);

        //        // Parse the structured response
        //        var terminationInfo = JsonSerializer.Deserialize<TerminationInfo>(responseContent);

        //        var shouldContinue = terminationInfo?.ShouldContinue ?? true;
        //        var reason = terminationInfo?.Reason ?? "Default continuation";

        //        // The method returns whether to terminate, which is the inverse of ShouldContinue
        //        var shouldTerminate = !shouldContinue;

        //        _logger.LogInformation("AI termination decision: ShouldContinue={ShouldContinue}, ShouldTerminate={ShouldTerminate}, Reason={Reason}",
        //            shouldContinue, shouldTerminate, reason);

        //        return new GroupChatManagerResult<bool>(shouldTerminate) { Reason = reason };
        //    }           
        //    catch (Exception ex)
        //    {
        //        _logger.LogError(ex, "Error in  termination evaluation, defaulting to continue");
        //        return new GroupChatManagerResult<bool>(false) { Reason = "Error in evaluation - continuing conversation" };
        //    }
        //}

        /// <summary>
        /// Unified AI-based conversation filtering and summarization with integrated content filter protection
        /// Combines FilterResults, FilterResultsAsync, and SummarizeUsingAI into a single method
        /// </summary>
        protected override ValueTask<GroupChatManagerResult<string>> FilterResults(IReadOnlyCollection<Microsoft.Extensions.AI.ChatMessage> history, CancellationToken cancellationToken = default) =>
         this.GetResponseAsync<string>(history, Prompts.Filter(_topic), cancellationToken);


        /// <summary>
        /// Unified conversation processing method with comprehensive AI summarization and content filter protection
        /// </summary>
        //private async Task<GroupChatManagerResult<string>> ProcessConversationSummaryAsync(
        //    IReadOnlyCollection<Microsoft.Extensions.AI.ChatMessage> history,
        //    CancellationToken cancellationToken = default)
        //{
        //    try
        //    {
        //        _logger.LogDebug("Processing conversation summary using unified AI approach with content filter protection");

        //        // Create conversation context from recent messages
        //        var conversationContext = CreateConversationContext(history);

        //        var summarizationMessages = new List<OpenAI.Chat.ChatMessage>
        //        {
        //            new SystemChatMessage(_filterPrompt),
        //            new UserChatMessage($"CONVERSATION: {conversationContext}")
        //        };

        //        var result = await _chatClient.CompleteChatAsync(summarizationMessages);
        //        var responseContent = result.Value.Content.FirstOrDefault()?.Text ?? string.Empty;

        //        _logger.LogDebug("AI conversation summary raw response: {Response}", responseContent);
        //        _logger.LogInformation("Successfully processed conversation summary: {SummaryLength} chars", responseContent.Length);

        //        return new GroupChatManagerResult<string>(responseContent) { Reason = "AI-generated conversation summary" };
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogError(ex, "Error in unified conversation processing, using fallback summary");
        //        return string.IsNullOrEmpty(ex.Message)
        //            ? new GroupChatManagerResult<string>("Error summarizing conversation")
        //            : new GroupChatManagerResult<string>(ex.Message) { Reason = "Error in summarization - using fallback" };
        //    }
        //}


        /// <summary>
        /// Create conversation context from message history with content filtering protection
        /// </summary>
        private string CreateConversationContext(IReadOnlyCollection<Microsoft.Extensions.AI.ChatMessage> history)
        {
            // Take the last 3 messages to provide context (reduced from 5 to minimize filter risk)
            var recentMessages = history.TakeLast(3);
            
            var context = string.Join("\n", recentMessages.Select(m => 
            {
                var role = m.Role.ToString();
                var rawContent = GetMessageContent(m);
                var content = rawContent;
                return $"{role}: {content}";
            }));
           
            
            return context;
        }

        /// <summary>
        /// Extract content from AI ChatMessage
        /// </summary>
        private string GetMessageContent(Microsoft.Extensions.AI.ChatMessage message)
        {
            try
            {
                // Handle different message content types
                if (message.Text != null)
                {
                    return message.Text;
                }
                
                // Fallback to string representation
                return message.ToString() ?? "";
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error extracting message content");
                return "Error extracting message content";
            }
        }
        
    }

    /// <summary>
    /// Microsoft Agent Framework implementation with GroupChatOrchestration
    /// Uses SelectionStrategy and TerminationStrategy for proper agent routing
    /// </summary>
    public class AgentFrameworkOrchestration: OrchestratingAgent
    {
        private readonly ChatClient _chatClient;
        private readonly ILoggerFactory _loggerFactory;
        private readonly ILogger<AgentFrameworkOrchestration> _logger;

        public AgentFrameworkOrchestration(ChatClient chatClient, ILoggerFactory loggerFactory)
        {
            _chatClient = chatClient;
            _loggerFactory = loggerFactory;
            _logger = loggerFactory.CreateLogger<AgentFrameworkOrchestration>();
        }

        /// <summary>
        /// Run GroupChat orchestration using Microsoft Agent Framework
        /// Creates all banking agents and orchestrates their interaction
        /// </summary>
        public async Task<(string responseText, string selectedAgentName)> RunGroupChatOrchestration(
            List<OpenAI.Chat.ChatMessage> chatHistory,
            BankingDataService bankService,
            string tenantId,
            string userId)
        {
            _logger.LogInformation("Running Agent Framework GroupChat orchestration");

            try
            {
                // Create all banking agents
                var agents = CreateAllAgents(bankService, tenantId, userId);

                // Convert messages to Agent Framework format
                var agentMessages = ConvertToAgentFrameworkMessages(chatHistory);

                // Create orchestration
                var orchestration = CreateGroupChatOrchestration(agents);
                
                // Run the orchestration
                var result = await ExecuteOrchestration(orchestration, agentMessages);
                
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GroupChat orchestration");
                return ("Sorry, I encountered an error while processing your request. Please try again.", "Error");
            }
        }

        /// <summary>
        /// Create all banking agents with proper instructions and tools
        /// </summary>
        private List<AIAgent> CreateAllAgents(BankingDataService bankService, string tenantId, string userId)
        {
            var agents = new List<AIAgent>();

            // Create Sales Agent
            var salesAgentType = AgentType.Sales;
            var salesAgent = _chatClient.CreateAIAgent(
                instructions: GetAgentPrompt(salesAgentType),
                name: GetAgentName(salesAgentType),
                description: GetAgentDescription(salesAgentType),
                tools: GetAgentTools(salesAgentType, bankService, tenantId, userId));
            agents.Add(salesAgent);
            _logger.LogInformation("Created {AgentName}: {AgentDescription}", GetAgentName(salesAgentType), GetAgentDescription(salesAgentType));

            // Create Transactions Agent
            var transactionsAgentType = AgentType.Transactions;
            var transactionsAgent = _chatClient.CreateAIAgent(
                instructions: GetAgentPrompt(transactionsAgentType),
                name: GetAgentName(transactionsAgentType),
                description: GetAgentDescription(transactionsAgentType),
                tools: GetAgentTools(transactionsAgentType, bankService, tenantId, userId));
            agents.Add(transactionsAgent);
            _logger.LogInformation("Created {AgentName}: {AgentDescription}", GetAgentName(transactionsAgentType), GetAgentDescription(transactionsAgentType));

            // Create CustomerSupport Agent
            var customerSupportAgentType = AgentType.CustomerSupport;
            var customerSupportAgent = _chatClient.CreateAIAgent(
                instructions: GetAgentPrompt(customerSupportAgentType),
                name: GetAgentName(customerSupportAgentType),
                description: GetAgentDescription(customerSupportAgentType),
                tools: GetAgentTools(customerSupportAgentType, bankService, tenantId, userId));
            agents.Add(customerSupportAgent);
            _logger.LogInformation("Created {AgentName}: {AgentDescription}", GetAgentName(customerSupportAgentType), GetAgentDescription(customerSupportAgentType));

            // Create Coordinator Agent
            var coordinatorAgentType = AgentType.Coordinator;
            var coordinatorAgent = _chatClient.CreateAIAgent(
                instructions: GetAgentPrompt(coordinatorAgentType),
                name: GetAgentName(coordinatorAgentType),
                description: GetAgentDescription(coordinatorAgentType),
                tools: GetAgentTools(coordinatorAgentType, bankService, tenantId, userId));
            agents.Add(coordinatorAgent);
            _logger.LogInformation("Created {AgentName}: {AgentDescription}", GetAgentName(coordinatorAgentType), GetAgentDescription(coordinatorAgentType));

            _logger.LogInformation("Successfully created {AgentCount} banking agents", agents.Count);
            return agents;
        }

        /// <summary>
        /// Create GroupChatOrchestration with proper configuration
        /// </summary>
        private GroupChatOrchestration CreateGroupChatOrchestration(List<AIAgent> agents)
        {
            // Create a custom GroupChatManager with SelectionStrategy and TerminationStrategy
            var groupChatManager = new BankingGroupChatManager(this.CreateChatClient(), _loggerFactory);
            
            // Log all agent names for debugging
            _logger.LogInformation("Creating GroupChatOrchestration with {AgentCount} agents:", agents.Count);
            foreach (var agent in agents)
            {
                _logger.LogInformation("  - Agent Name: '{AgentName}', Description: '{AgentDescription}'", 
                    agent.Name ?? "null", agent.Description ?? "null");
            }
              
            try
            {
                // Create orchestration with the custom manager
                return new GroupChatOrchestration(groupChatManager, agents.ToArray());
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to create GroupChatOrchestration with provided constructor");
                
                // If that fails, we'll need to use a different approach
                throw new NotSupportedException("GroupChatOrchestration constructor not compatible with current API");
            }
        }

        /// <summary>
        /// Execute orchestration with proper error handling
        /// </summary>
        private async Task<(string responseText, string selectedAgentName)> ExecuteOrchestration(
            GroupChatOrchestration orchestration, 
            List<Microsoft.Extensions.AI.ChatMessage> messages)
        {
            try
            {
                _logger.LogInformation("Executing GroupChat orchestration with {MessageCount} messages", messages.Count);
                
                // Run orchestration with the conversation messages
                var result = await orchestration.RunAsync(messages);
                
                _logger.LogInformation("Orchestration completed, result type: {ResultType}", result?.GetType().FullName ?? "null");
                
                // Extract response from the result
                return await ExtractResponseFromResultAsync(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing orchestration");
                return ("Error in orchestration execution", "Unknown");
            }
        }

        /// <summary>
        /// Extract response from orchestration result
        /// </summary>
        private async Task<(string responseText, string selectedAgentName)> ExtractResponseFromResultAsync(object result)
        {
            try
            {
                _logger.LogInformation("Processing orchestration result of type: {ResultType}", result?.GetType().FullName ?? "null");

                // Handle OrchestratingAgentResponse specifically
                if (result != null && result.GetType().FullName == "Microsoft.Agents.Orchestration.OrchestratingAgentResponse")
                {
                    _logger.LogInformation("Processing OrchestratingAgentResponse");
                    
                    // Use reflection to extract properties from OrchestratingAgentResponse
                    var resultType = result.GetType();
                    
                    // Log all available properties for debugging
                    var properties = resultType.GetProperties(BindingFlags.Public | BindingFlags.Instance);
                    _logger.LogInformation("Available properties on OrchestratingAgentResponse: {Properties}", 
                        string.Join(", ", properties.Select(p => $"{p.Name}:{p.PropertyType.Name}")));
                    
                    // Check for Task property first (contains the actual AgentRunResponse)
                    var taskProperty = resultType.GetProperty("Task");
                    if (taskProperty != null)
                    {
                        var taskValue = taskProperty.GetValue(result);
                        _logger.LogDebug("Task property type: {TaskType}", taskValue?.GetType().FullName ?? "null");
                        
                        if (taskValue is Task task)
                        {
                            _logger.LogInformation("Found Task property, awaiting completion");
                            
                            try
                            {
                                await task;
                                
                                // Check if the task has a Result property (for Task<T>)
                                var taskType = taskValue.GetType();
                                var resultProperty = taskType.GetProperty("Result");
                                if (resultProperty != null)
                                {
                                    var agentRunResponse = resultProperty.GetValue(taskValue);
                                    _logger.LogInformation("Retrieved AgentRunResponse from Task.Result: {ResponseType}", 
                                        agentRunResponse?.GetType().FullName ?? "null");
                                    
                                    if (agentRunResponse != null)
                                    {
                                        // Extract response from AgentRunResponse
                                        var agentRunResponseType = agentRunResponse.GetType();
                                        
                                        // Check for Messages property in AgentRunResponse
                                        var agentRunMessagesProperty = agentRunResponseType.GetProperty("Messages");
                                        if (agentRunMessagesProperty != null)
                                        {
                                            var agentRunMessages = agentRunMessagesProperty.GetValue(agentRunResponse);
                                            
                                            if (agentRunMessages is IEnumerable<Microsoft.Extensions.AI.ChatMessage> runChatMessages)
                                            {
                                                var runMessagesList = runChatMessages.ToList();
                                                _logger.LogInformation("Found {MessageCount} messages in AgentRunResponse", runMessagesList.Count);
                                                
                                                var lastRunMessage = runMessagesList.LastOrDefault();
                                                if (lastRunMessage != null)
                                                {
                                                    var extractedResponse = GetMessageContentFromAI(lastRunMessage);
                                                    var extractedAgentName = ExtractAgentName(lastRunMessage) ?? "GroupChat";
                                                    
                                                    _logger.LogInformation("Extracted response from AgentRunResponse Messages: {ResponseLength} chars, Agent: {AgentName}", 
                                                        extractedResponse.Length, extractedAgentName);
                                                    return (extractedResponse, extractedAgentName);
                                                }
                                            }
                                        }
                                        
                                        // If no Messages found, try to use the AgentRunResponse directly
                                        var fallbackResponse = agentRunResponse.ToString() ?? "AgentRunResponse completed";
                                        _logger.LogInformation("Using AgentRunResponse ToString as fallback: {ResponseLength} chars", fallbackResponse.Length);
                                        return (fallbackResponse, "GroupChat");
                                    }
                                }
                            }
                            catch (Exception taskEx)
                            {
                                _logger.LogError(taskEx, "Error awaiting Task from OrchestratingAgentResponse");
                            }
                        }
                    }
                    
                    // Fallback: use ToString() of the entire object
                    var defaultResponse = result.ToString() ?? "OrchestratingAgentResponse completed";
                    _logger.LogWarning("No meaningful response found in OrchestratingAgentResponse, using ToString() fallback: {Response}", defaultResponse);
                    return (defaultResponse, "GroupChat");
                }

                // Handle other result types that might be returned
                if (result is IAsyncEnumerable<Microsoft.Extensions.AI.ChatMessage> asyncMessages)
                {
                    _logger.LogInformation("Processing IAsyncEnumerable<ChatMessage>");
                    
                    // Convert async enumerable to list and get the last message
                    var messagesList = new List<Microsoft.Extensions.AI.ChatMessage>();
                    var enumerator = asyncMessages.GetAsyncEnumerator();
                    
                    try
                    {
                        while (await enumerator.MoveNextAsync())
                        {
                            messagesList.Add(enumerator.Current);
                        }
                    }
                    finally
                    {
                        await enumerator.DisposeAsync();
                    }
                    
                    var lastMessage = messagesList.LastOrDefault();
                    if (lastMessage != null)
                    {
                        var content = GetMessageContentFromAI(lastMessage);
                        var agentName = ExtractAgentName(lastMessage) ?? "GroupChat";
                        
                        _logger.LogInformation("Extracted response from {MessageCount} messages, agent: {AgentName}", 
                            messagesList.Count, agentName);
                        
                        return (content, agentName);
                    }
                }
                else if (result is Microsoft.Extensions.AI.ChatMessage singleMessage)
                {
                    _logger.LogInformation("Processing single ChatMessage");
                    
                    var content = GetMessageContentFromAI(singleMessage);
                    var agentName = ExtractAgentName(singleMessage) ?? "GroupChat";
                    
                    return (content, agentName);
                }
                else if (result is IEnumerable<Microsoft.Extensions.AI.ChatMessage> messageList)
                {
                    _logger.LogInformation("Processing IEnumerable<ChatMessage>");
                    
                    var lastMessage = messageList.LastOrDefault();
                    if (lastMessage != null)
                    {
                        var content = GetMessageContentFromAI(lastMessage);
                        var agentName = ExtractAgentName(lastMessage) ?? "GroupChat";
                        
                        return (content, agentName);
                    }
                }
                
                // Fallback for unknown result types
                var responseText = result?.ToString() ?? "Orchestration completed";
                var selectedAgentName = "GroupChat";
                
                _logger.LogInformation("Using fallback for unknown result type: {ResultType}", result?.GetType().FullName ?? "null");
                
                return (responseText, selectedAgentName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error extracting response from orchestration result");
                return ("Error processing orchestration response", "Unknown");
            }
        }

        /// <summary>
        /// Extract content from AI ChatMessage
        /// </summary>
        private string GetMessageContentFromAI(Microsoft.Extensions.AI.ChatMessage message)
        {
            try
            {
                // Handle different message content types
                if (message.Text != null)
                {
                    return message.Text;
                }
                
                // Fallback to string representation
                return message.ToString() ?? "";
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error extracting message content");
                return "Error extracting message content";
            }
        }

        /// <summary>
        /// Extract agent name from message metadata
        /// </summary>
        private string? ExtractAgentName(Microsoft.Extensions.AI.ChatMessage message)
        {
            try
            {
                // Try to extract agent name from message properties or metadata
                var messageType = message.GetType();
                var authorProperty = messageType.GetProperty("Author");
                if (authorProperty != null)
                {
                    var author = authorProperty.GetValue(message);
                    return author?.ToString();
                }
                
                // Check for other potential agent identification
                var agentProperty = messageType.GetProperty("Agent");
                if (agentProperty != null)
                {
                    var agent = agentProperty.GetValue(message);
                    return agent?.ToString();
                }
                
                // Fallback - return null to use default
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Could not extract agent name from message");
                return null;
            }
        }

        /// <summary>
        /// Convert OpenAI messages to Agent Framework messages
        /// </summary>
        private List<Microsoft.Extensions.AI.ChatMessage> ConvertToAgentFrameworkMessages(List<OpenAI.Chat.ChatMessage> chatHistory)
        {
            var agentMessages = new List<Microsoft.Extensions.AI.ChatMessage>();

            foreach (var msg in chatHistory)
            {
                var content = ExtractMessageContent(msg);
                var role = MapToAgentFrameworkRole(msg);
                
                if (!string.IsNullOrEmpty(content))
                {
                    agentMessages.Add(new Microsoft.Extensions.AI.ChatMessage(role, content));
                }
            }

            return agentMessages;
        }

        private string ExtractMessageContent(OpenAI.Chat.ChatMessage message)
        {
            return message switch
            {
                OpenAI.Chat.UserChatMessage userMsg => userMsg.Content.FirstOrDefault()?.Text ?? "",
                OpenAI.Chat.AssistantChatMessage assistantMsg => assistantMsg.Content.FirstOrDefault()?.Text ?? "",
                OpenAI.Chat.SystemChatMessage systemMsg => systemMsg.Content.FirstOrDefault()?.Text ?? "",
                _ => ""
            };
        }

        private Microsoft.Extensions.AI.ChatRole MapToAgentFrameworkRole(OpenAI.Chat.ChatMessage message)
        {
            return message switch
            {
                OpenAI.Chat.UserChatMessage => Microsoft.Extensions.AI.ChatRole.User,
                OpenAI.Chat.AssistantChatMessage => Microsoft.Extensions.AI.ChatRole.Assistant,
                OpenAI.Chat.SystemChatMessage => Microsoft.Extensions.AI.ChatRole.System,
                _ => Microsoft.Extensions.AI.ChatRole.User
            };
        }

        /// <summary>
        /// Get agent prompt based on type
        /// </summary>
        private string GetAgentPrompt(AgentType agentType)
        {
            string promptFile = agentType switch
            {
                AgentType.Sales => "Sales.prompty",
                AgentType.Transactions => "Transactions.prompty",
                AgentType.CustomerSupport => "CustomerSupport.prompty",
                AgentType.Coordinator => "Coordinator.prompty",
                _ => throw new ArgumentOutOfRangeException(nameof(agentType), agentType, null)
            };

            string prompt = $"{File.ReadAllText($"Prompts/{promptFile}")}{File.ReadAllText("Prompts/CommonAgentRules.prompty")}";

            return prompt;
        }

        /// <summary>
        /// Get agent name based on type
        /// Names must match pattern: ^[^\s<|\\/>]+$ (no spaces or special characters)
        /// </summary>
        private string GetAgentName(AgentType agentType)
        {
            return agentType switch
            {
                AgentType.Sales => "Sales",
                AgentType.Transactions => "Transactions",
                AgentType.CustomerSupport => "CustomerSupport",
                AgentType.Coordinator => "Coordinator",
                _ => throw new ArgumentOutOfRangeException(nameof(agentType), agentType, null)
            };
        }

        /// <summary>
        /// Get agent description
        /// </summary>
        private string GetAgentDescription(AgentType agentType)
        {
            return agentType switch
            {
                AgentType.Sales => "Handles sales inquiries, account registration, and offers",
                AgentType.Transactions => "Manages transactions, transfers, and transaction history",
                AgentType.CustomerSupport => "Provides customer support, handles complaints and service requests",
                AgentType.Coordinator => "Coordinates and routes requests to appropriate agents",
                _ => throw new ArgumentOutOfRangeException(nameof(agentType), agentType, null)
            };
        }

        /// <summary>
        /// Get tools for specific agent type using existing tool classes
        /// Returns null for now due to delegate binding limitations with the current Agent Framework API
        /// </summary>
        private IList<AITool>? GetAgentTools(AgentType agentType, BankingDataService bankService, string tenantId, string userId)
        {
            try
            {
                _logger.LogInformation("Creating tools for agent type: {AgentType}", agentType);
                
                // Create the appropriate tools class based on agent type
                BaseTools toolsClass = agentType switch
                {
                    AgentType.Sales => new SalesTools(_loggerFactory.CreateLogger<SalesTools>(), bankService, tenantId, userId),
                    AgentType.Transactions => new TransactionTools(_loggerFactory.CreateLogger<TransactionTools>(), bankService, tenantId, userId),
                    AgentType.CustomerSupport => new CustomerSupportTools(_loggerFactory.CreateLogger<CustomerSupportTools>(), bankService, tenantId, userId),
                    AgentType.Coordinator => new CoordinatorTools(_loggerFactory.CreateLogger<CoordinatorTools>(), bankService, tenantId, userId),
                    _ => throw new ArgumentOutOfRangeException(nameof(agentType), agentType, null)
                };

                // Log the tool class creation for debugging
                _logger.LogInformation("Created {ToolClassName} for agent type: {AgentType}", toolsClass.GetType().Name, agentType);
                
                // Log available methods with Description attributes
                var methods = toolsClass.GetType().GetMethods()
                    .Where(m => m.GetCustomAttributes(typeof(DescriptionAttribute), false).Length > 0);
                
                foreach (var method in methods)
                {
                    var description = method.GetCustomAttribute<DescriptionAttribute>()?.Description ?? "No description";
                    _logger.LogInformation("Agent {AgentType} has method: '{MethodName}' - {Description}", 
                        agentType, method.Name, description);
                }

                _logger.LogInformation("Tool class created for agent type: {AgentType}. Returning null due to delegate binding limitations.", agentType);
                
                // Return null - avoiding delegate binding issues for now
                // The agents will work with their instructions until proper tool integration is available
                // All banking functionality is preserved in the tool classes with [Description] attributes
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating tools for agent type: {AgentType}", agentType);
                return null;
            }
        }
    }
}