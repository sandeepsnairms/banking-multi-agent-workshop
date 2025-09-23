
namespace  MultiAgentCopilot.Models.Configuration
{
    public record AgentFrameworkServiceSettings
    {        
        public AzureOpenAISettings AzureOpenAISettings { get; init; }
        //public required CosmosDBSettings CosmosDBVectorStoreSettings { get; init; }
    }
}