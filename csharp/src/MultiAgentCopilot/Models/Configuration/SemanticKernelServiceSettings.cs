using System.ComponentModel.DataAnnotations;

namespace  MultiAgentCopilot.Models.Configuration
{
    public record SemanticKernelServiceSettings
    {
        [Required]
        public required AzureOpenAISettings AzureOpenAISettings { get; init; }
        //public required CosmosDBSettings CosmosDBVectorStoreSettings { get; init; }
    }
}