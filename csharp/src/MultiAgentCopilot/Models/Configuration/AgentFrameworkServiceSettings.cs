namespace MultiAgentCopilot.Models.Configuration
{
    public record AgentFrameworkServiceSettings
    {        
        public required AzureOpenAISettings AzureOpenAISettings { get; init; }
    }
}