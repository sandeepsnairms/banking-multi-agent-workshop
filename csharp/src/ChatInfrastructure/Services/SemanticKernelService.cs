using MultiAgentCopilot.Common.Models.Chat;
using MultiAgentCopilot.ChatInfrastructure.Interfaces;
using MultiAgentCopilot.Common.Models.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using MultiAgentCopilot.ChatInfrastructure.Helper;
using Microsoft.SemanticKernel.ChatCompletion;

using Azure.Identity;
using MultiAgentCopilot.ChatInfrastructure.Factories;
using Newtonsoft.Json;
using System.Data;
using MultiAgentCopilot.Common.Models.Debug;
using BankingServices.Interfaces;
using Microsoft.SemanticKernel.Embeddings;
using System.Runtime;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.VectorData;
using Microsoft.SemanticKernel.Connectors.AzureCosmosDBNoSQL;
using MultiAgentCopilot.Common.Models.Banking;


using  Microsoft.Extensions.AI;
using OllamaSharp;

namespace MultiAgentCopilot.ChatInfrastructure.Services;

public class SemanticKernelService : ISemanticKernelService, IDisposable
{
    readonly SemanticKernelServiceSettings _skSettings;
    readonly ILoggerFactory _loggerFactory;
    readonly ILogger<SemanticKernelService> _logger;
    readonly Kernel _semanticKernel;


    bool _serviceInitialized = false;
    string _prompt = string.Empty;
    string _contextSelectorPrompt = string.Empty;

    List<LogProperty> _promptDebugProperties;

    public bool IsInitialized => _serviceInitialized;

    public SemanticKernelService(
        IOptions<SemanticKernelServiceSettings> skOptions,
        ILoggerFactory loggerFactory)
    {
        _skSettings = skOptions.Value;
        _loggerFactory = loggerFactory;
        _logger = _loggerFactory.CreateLogger<SemanticKernelService>();
        _promptDebugProperties = new List<LogProperty>();

        _logger.LogInformation("Initializing the Semantic Kernel service...");

        var builder = Kernel.CreateBuilder();

        builder.Services.AddSingleton<ILoggerFactory>(loggerFactory);

        DefaultAzureCredential credential;
        if (string.IsNullOrEmpty(_skSettings.AzureOpenAISettings.UserAssignedIdentityClientID))
        {
            credential = new DefaultAzureCredential();
        }
        else
        {
            credential = new DefaultAzureCredential(new DefaultAzureCredentialOptions
            {
                ManagedIdentityClientId = _skSettings.AzureOpenAISettings.UserAssignedIdentityClientID
            });
        }


        var ollamaClient = new OllamaApiClient(
            uriString: _skSettings.OllamaSettings.Endpoint,
            defaultModel: _skSettings.OllamaSettings.CompletionsDeployment);

        var chatService = ollamaClient.AsChatCompletionService();

#pragma warning disable SKEXP0070 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
        builder.AddOllamaChatCompletion(ollamaClient);
#pragma warning restore SKEXP0070 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.

        // Register your custom chat completion service (Ollama)
        //builder.Services.AddSingleton<IChatCompletionService>(chatService);

        //builder.AddAzureOpenAIChatCompletion(
        //    _skSettings.AzureOpenAISettings.CompletionsDeployment,
        //    _skSettings.AzureOpenAISettings.Endpoint,
        //    credential);

        builder.AddAzureOpenAITextEmbeddingGeneration(
               _skSettings.AzureOpenAISettings.EmbeddingsDeployment,
               _skSettings.AzureOpenAISettings.Endpoint,
               credential);

        _semanticKernel = builder.Build();

        Task.Run(Initialize).ConfigureAwait(false);
    }

    private Task Initialize()
    {
        try
        {
            _serviceInitialized = true;
            _logger.LogInformation("Semantic Kernel service initialized.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Semantic Kernel service was not initialized. The following error occurred: {ErrorMessage}.", ex.ToString());
        }
        return Task.CompletedTask;
    }

    private void LogMessage(string key, string value)
    {
        _promptDebugProperties.Add(new LogProperty(key, value));
    }

    public async Task<Tuple<List<Message>, List<DebugLog>>> GetResponse(Message userMessage, List<Message> messageHistory, IBankDataService bankService, string tenantId, string userId)
    {
        try
        {
            ChatFactory multiAgentChatGeneratorService = new ChatFactory();

            var agentGroupChat = multiAgentChatGeneratorService.BuildAgentGroupChat(_semanticKernel, _loggerFactory, LogMessage, bankService, tenantId, userId);

            // Load history
            foreach (var chatMessage in messageHistory)
            {
                AuthorRole? role = AuthorRoleHelper.FromString(chatMessage.SenderRole);
                var chatMessageContent = new ChatMessageContent
                {
                    Role = role ?? AuthorRole.User,
                    Content = chatMessage.Text
                };
                agentGroupChat.AddChatMessage(chatMessageContent);
            }

            _promptDebugProperties = new List<LogProperty>();

            List<Message> completionMessages = new();
            List<DebugLog> completionMessagesLogs = new();
            do
            {
                var userResponse = new ChatMessageContent(AuthorRole.User, userMessage.Text);
                agentGroupChat.AddChatMessage(userResponse);

                agentGroupChat.IsComplete = false;

                await foreach (ChatMessageContent response in agentGroupChat.InvokeAsync())
                {
                    string messageId = Guid.NewGuid().ToString();
                    string debugLogId = Guid.NewGuid().ToString();
                    completionMessages.Add(new Message(userMessage.TenantId, userMessage.UserId, userMessage.SessionId, response.AuthorName ?? string.Empty, response.Role.ToString(), response.Content ?? string.Empty, messageId, debugLogId));

                    if (_promptDebugProperties.Count > 0)
                    {
                        var completionMessagesLog = new DebugLog(userMessage.TenantId, userMessage.UserId, userMessage.SessionId, messageId, debugLogId);
                        completionMessagesLog.PropertyBag = _promptDebugProperties;
                        completionMessagesLogs.Add(completionMessagesLog);
                    }

                }
            }
            while (!agentGroupChat.IsComplete);
            //_promptDebugProperties.Clear();

            return new Tuple<List<Message>, List<DebugLog>>(completionMessages, completionMessagesLogs);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error when getting response: {ErrorMessage}", ex.ToString());
            return new Tuple<List<Message>, List<DebugLog>>(new List<Message>(), new List<DebugLog>());
        }
    }


    public async Task<string> Summarize(string sessionId, string userPrompt)
    {
        try
        {
            // Use an AI function to summarize the text in 2 words
            var summarizeFunction = _semanticKernel.CreateFunctionFromPrompt(
                "Summarize the following text into exactly two words:\n\n{{$input}}",
                executionSettings: new OpenAIPromptExecutionSettings { MaxTokens = 10 }
            );

            // Invoke the function
            var summary = await _semanticKernel.InvokeAsync(summarizeFunction, new() { ["input"] = userPrompt });

            return summary.GetValue<string>() ?? "No summary generated";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error when getting response: {ErrorMessage}", ex.ToString());
            return string.Empty;
        }
    }



    public async Task<float[]> GenerateEmbedding(string text)
    {
        // Generate Embedding

        var embeddingModel = _semanticKernel.Services.GetRequiredService<ITextEmbeddingGenerationService>();

        var embedding = await embeddingModel.GenerateEmbeddingAsync(text);

        // Convert ReadOnlyMemory<float> to IList<float>
        return embedding.ToArray();
    }


#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
    public async Task ResetSemanticCache()
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
    {
        // Implementation for resetting the semantic cache
    }

    public void Dispose()
    {
        // Dispose resources if any
    }
}