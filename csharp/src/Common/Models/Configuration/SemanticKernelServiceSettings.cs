
namespace MultiAgentCopilot.Common.Models.Configuration
{
    public record SemanticKernelServiceSettings
    {
        public required AzureOpenAISettings AzureOpenAISettings { get; init; }
    }
}