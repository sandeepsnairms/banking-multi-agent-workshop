using MultiAgentCopilot.Common.Models.Configuration;
using MultiAgentCopilot.Common.Models.ConfigurationOptions;
using MultiAgentCopilot.ChatInfrastructure.Models.ConfigurationOptions;

namespace MultiAgentCopilot.ChatInfrastructure.Models.ConfigurationOptions
{
    public record SemanticKernelServiceSettings
    {
        public required AzureOpenAISettings AzureOpenAISettings { get; init; }
        public required CosmosDBSettings CosmosDBVectorStoreSettings { get; init; }
    }
}