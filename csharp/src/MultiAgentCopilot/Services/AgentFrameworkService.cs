

using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Agents.Orchestration;
using Microsoft.Extensions.AI;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.OpenAI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MultiAgentCopilot.Factories;
using MultiAgentCopilot.Models;
using MultiAgentCopilot.Models.Chat;
using MultiAgentCopilot.Models.ChatInfoFormats;
using MultiAgentCopilot.Models.Configuration;
using MultiAgentCopilot.Models.Debug;
using MultiAgentCopilot.MultiAgentCopilot.Factories;
using MultiAgentCopilot.MultiAgentCopilot.Helper;
using MultiAgentCopilot.MultiAgentCopilot.Services;
using MultiAgentCopilot.Services;

using OpenAI;
//using Azure.AI.OpenAI;
using OpenAI.Chat;
using System.Text.Json;
//using Azure.AI.OpenAI;


namespace MultiAgentCopilot.Services;

public class AgentFrameworkService : IDisposable
{
    private readonly AgentFrameworkServiceSettings _settings;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<AgentFrameworkService> _logger;
    private readonly OpenAI.Chat.ChatClient _chatClient;
    private BankingDataService _bankService;
    private MCPToolService _mcpService;

    private bool _serviceInitialized = false;
    private List<LogProperty> _promptDebugProperties;
    private List<Microsoft.Agents.AI.AIAgent> _agents = null;

    public bool IsInitialized => _serviceInitialized;

    public AgentFrameworkService(
        IOptions<AgentFrameworkServiceSettings> options,
        ILoggerFactory loggerFactory)
    {
        _settings = options.Value;
        _loggerFactory = loggerFactory;
        _logger = _loggerFactory.CreateLogger<AgentFrameworkService>();
        _promptDebugProperties = new List<LogProperty>();

        _logger.LogInformation("Initializing the Agent Framework service...");

        // Create Azure OpenAI chat client with improved credential handling

        DefaultAzureCredential credential;
        if (string.IsNullOrEmpty(_settings.AzureOpenAISettings.UserAssignedIdentityClientID))
        {
            credential = new DefaultAzureCredential();
        }
        else
        {
            credential = new DefaultAzureCredential(new DefaultAzureCredentialOptions
            {
                ManagedIdentityClientId = _settings.AzureOpenAISettings.UserAssignedIdentityClientID
            });
        }


        var endpoint = new Uri(_settings.AzureOpenAISettings.Endpoint);

        var openAIClient = new AzureOpenAIClient(endpoint, credential);
        // Use ChatClient directly from OpenAI SDK
        _chatClient = openAIClient.GetChatClient(_settings.AzureOpenAISettings.CompletionsDeployment);

             

        _logger.LogWarning("Agent Framework Initialized.");

        Task.Run(Initialize).ConfigureAwait(false);
    }

    private void LogMessage(string key, string value)
    {
        _promptDebugProperties.Add(new LogProperty(key, value));
    }


    public bool SetInProcessToolService(BankingDataService bankService)
    {
         _bankService = bankService ?? throw new ArgumentNullException(nameof(bankService));

        _logger.LogInformation("InProcessToolService has been set.");
        return true;
    }


    public bool SetMCPToolService(MCPToolService mcpService)
    {
        _mcpService = mcpService ?? throw new ArgumentNullException(nameof(mcpService));

        _logger.LogInformation("MCPToolService has been set.");
        return true;
    }

    public bool InitializeAgents()
    {
        if (_agents == null || _agents.Count == 0)
        {
            if(_mcpService != null) 
                _agents = AgentFactory.CreateAllAgentsWithMCPToolsAsync(_chatClient, _mcpService, _loggerFactory).GetAwaiter().GetResult();

            if(_bankService != null)
                _agents = AgentFactory.CreateAllAgentsWithInProcessTools(_chatClient, _bankService, _loggerFactory);

            if (_agents == null || _agents.Count == 0)
            {
                _logger.LogError("No agents available for orchestration");
               return false;
            }
        }
        return true;
    }


    public async Task<Tuple<List<Message>, List<DebugLog>>> GetResponse(
        Message userMessage,
        List<Message> messageHistory,
        BankingDataService bankService,
        string tenantId,
        string userId)
    {
        try
        {
            // Convert message history to ChatMessage format
            var chatHistory = new List<Microsoft.Extensions.AI.ChatMessage>();
            foreach (var msg in messageHistory)
            {
                var role = msg.SenderRole.ToLowerInvariant() switch
                {
                    "user" => ChatMessageRole.User,
                    "assistant" => ChatMessageRole.Assistant,
                    "system" => ChatMessageRole.System,
                    _ => ChatMessageRole.User
                };

                if (role == ChatMessageRole.User)
                {
                    chatHistory.Add(new Microsoft.Extensions.AI.ChatMessage(ChatRole.User, msg.Text));
                }
                else if (role == ChatMessageRole.Assistant)
                {
                    chatHistory.Add(new Microsoft.Extensions.AI.ChatMessage(ChatRole.Assistant, msg.Text));
                }
                else if (role == ChatMessageRole.System)
                {
                    chatHistory.Add(new Microsoft.Extensions.AI.ChatMessage(ChatRole.System, msg.Text));
                }
            }

            // Add user message
            chatHistory.Add(new Microsoft.Extensions.AI.ChatMessage(ChatRole.User, userMessage.Text));

            _promptDebugProperties.Clear();

            // Use AI-based GroupChat orchestration with selection and termination strategies
            var (responseText, selectedAgentName) = await RunGroupChatOrchestration(chatHistory,  tenantId, userId);

            var completionMessages = new List<Message>();
            var completionMessagesLogs = new List<DebugLog>();

            string messageId = Guid.NewGuid().ToString();
            string debugLogId = Guid.NewGuid().ToString();

            var responseMessage = new Message(
                userMessage.TenantId,
                userMessage.UserId,
                userMessage.SessionId,
                selectedAgentName,
                "Assistant",
                responseText,
                messageId,
                debugLogId);

            completionMessages.Add(responseMessage);

            if (_promptDebugProperties.Count > 0)
            {
                var debugLog = new DebugLog(userMessage.TenantId, userMessage.UserId, userMessage.SessionId, messageId, debugLogId)
                {
                    PropertyBag = _promptDebugProperties.ToList()
                };
                completionMessagesLogs.Add(debugLog);
            }

            return new Tuple<List<Message>, List<DebugLog>>(completionMessages, completionMessagesLogs);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error when getting response: {ErrorMessage}", ex.ToString());
            return new Tuple<List<Message>, List<DebugLog>>(new List<Message>(), new List<DebugLog>());
        }
    }

    private async Task<(string responseText, string selectedAgentName)> RunGroupChatOrchestration(
        List<Microsoft.Extensions.AI.ChatMessage> chatHistory,
        string tenantId,
        string userId)
    {
        try
        {
            _logger.LogInformation("Starting Agent Framework orchestration");

            //OrchestrationMonitor monitor = new();
            //monitor.History.AddRange(chatHistory);
                     
            // Create a custom GroupChatManager with SelectionStrategy and TerminationStrategy
            var groupChatManager = new GroupChatManagerFactory(chatHistory.Last().Text, _chatClient, LogMessage);

            GroupChatOrchestration groupChatOrchestration = new GroupChatOrchestration(groupChatManager, _agents.ToArray());
            //{
            //    LoggerFactory = this._loggerFactory,
            //    ResponseCallback = monitor.ResponseCallbackAsync,
            //    StreamingResponseCallback = monitor.StreamingResultCallback
            //};
            var orchestrationResponse = await groupChatOrchestration.RunAsync(chatHistory);
            AgentRunResponse result = await orchestrationResponse.Task;

            string responseText = result.Text;
            // In RunGroupChatOrchestration, ensure selectedAgentName is not null
            string selectedAgentName = result.AgentId ?? "Unknown";

            _logger.LogInformation("Agent Framework orchestration completed with agent: {AgentName}", selectedAgentName);

            return (responseText, selectedAgentName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in Agent Framework orchestration");
            return ("Sorry, I encountered an error while processing your request. Please try again.", "Error");
        }
    }

    



    //private AgentType ParseAgentName(string agentName)
    //{
    //    return agentName.ToLowerInvariant() switch
    //    {
    //        "sales" => AgentType.Sales,
    //        "transactions" => AgentType.Transactions,
    //        "customersupport" => AgentType.CustomerSupport,
    //        "coordinator" => AgentType.Coordinator,
    //        _ => AgentType.Coordinator
    //    };
    //}

    public async Task<string> Summarize(string sessionId, string userPrompt)
    {
        try
        {
            var agent = _chatClient.CreateAIAgent("Summarize the following text into exactly two words:", "Summarizer", "A tool to summarize text", new AIFunction[] { });

            await foreach (var update in agent.RunStreamingAsync(userPrompt))
            {
                return update.Text;
            }

            return "No summary generated";

        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error when getting response: {ErrorMessage}", ex.ToString());
            return string.Empty;
        }
    }

    private Task Initialize()
    {
        try
        {
            _serviceInitialized = true;
            _logger.LogInformation("Agent Framework service initialized.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Agent Framework service was not initialized. The following error occurred: {ErrorMessage}.", ex.ToString());
        }
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        // Dispose resources if any
    }
}