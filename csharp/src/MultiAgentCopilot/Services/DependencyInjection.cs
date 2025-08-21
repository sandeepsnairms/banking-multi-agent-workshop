using  MultiAgentCopilot.Models.Configuration;
using MultiAgentCopilot.Services;
using MultiAgentCopilot.Monitoring;

namespace MultiAgentCopilot
{
    /// <summary>
    /// General purpose dependency injection extensions.
    /// </summary>
    public static partial class DependencyInjection
    {
        
        public static void AddSemanticKernelService(this IHostApplicationBuilder builder)
        {
            // Debug configuration binding
            var semanticKernelSection = builder.Configuration.GetSection("SemanticKernelServiceSettings");
            Console.WriteLine($"SemanticKernelServiceSettings section exists: {semanticKernelSection.Exists()}");
            
            if (semanticKernelSection.Exists())
            {
                var azureOpenAISection = semanticKernelSection.GetSection("AzureOpenAISettings");
                Console.WriteLine($"AzureOpenAISettings section exists: {azureOpenAISection.Exists()}");
                Console.WriteLine($"Endpoint: {azureOpenAISection["Endpoint"]}");
                Console.WriteLine($"CompletionsDeployment: {azureOpenAISection["CompletionsDeployment"]}");
                Console.WriteLine($"EmbeddingsDeployment: {azureOpenAISection["EmbeddingsDeployment"]}");
            }

            builder.Services.AddOptions<SemanticKernelServiceSettings>()
                .Bind(builder.Configuration.GetSection("SemanticKernelServiceSettings"))
                .ValidateDataAnnotations()
                .ValidateOnStart();
                
            builder.Services.AddSingleton<AgentOrchestrationService>();
            
            // Add OrchestrationMonitor for monitoring multi-agent conversations
            builder.Services.AddSingleton<OrchestrationMonitor>();
        }

        public static void AddCosmosDBService(this IHostApplicationBuilder builder)
        {
            // Debug configuration binding
            var cosmosSection = builder.Configuration.GetSection("CosmosDBSettings");
            Console.WriteLine($"CosmosDBSettings section exists: {cosmosSection.Exists()}");
            Console.WriteLine($"CosmosUri: {cosmosSection["CosmosUri"]}");
            Console.WriteLine($"Database: {cosmosSection["Database"]}");

            builder.Services.AddOptions<CosmosDBSettings>()
                .Bind(builder.Configuration.GetSection("CosmosDBSettings"))
                .ValidateDataAnnotations()
                .ValidateOnStart();

            Console.WriteLine("Adding CosmosDBService:" + builder.Configuration["CosmosDBSettings:CosmosUri"]);
            builder.Services.AddSingleton<CosmosDBService>();
        }

        public static void AddChatService(this IHostApplicationBuilder builder)
        {
            builder.Services.AddOptions<CosmosDBSettings>()
                .Bind(builder.Configuration.GetSection("CosmosDBSettings"));
            builder.Services.AddSingleton<ChatService>();
        }

    }
}
