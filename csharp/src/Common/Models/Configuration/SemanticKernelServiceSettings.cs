
namespace MultiAgentCopilot.Common.Models.Configuration
{
    public record SemanticKernelServiceSettings
    {
        public OllamaSettings OllamaSettings { get; init; }
        public AzureOpenAISettings AzureOpenAISettings { get; init; }
        public required CosmosDBSettings CosmosDBVectorStoreSettings { get; init; }
    }
}