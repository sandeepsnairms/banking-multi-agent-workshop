using MultiAgentCopilot.Models.Configuration;
using MultiAgentCopilot.MultiAgentCopilot.Services;
using MultiAgentCopilot.Services;

namespace MultiAgentCopilot
{
    /// <summary>
    /// General purpose dependency injection extensions.
    /// </summary>
    public static partial class DependencyInjection
    {

   

        public static void AddAgentFrameworkService(this IHostApplicationBuilder builder)
        {
            builder.Services.AddOptions<AgentFrameworkServiceSettings>()
                .Bind(builder.Configuration.GetSection("AgentFrameworkServiceSettings"));
            builder.Services.AddSingleton<AgentFrameworkService>();
        }

        public static void AddMCPService(this IHostApplicationBuilder builder)
        {
            builder.Services.AddOptions<MCPSettings>()
                .Bind(builder.Configuration.GetSection("AgentFrameworkServiceSettings").GetSection("MCPSettings"));
            builder.Services.AddSingleton<MCPToolService>();
        }

        public static void AddCosmosDBService(this IHostApplicationBuilder builder)
        {
            builder.Services.AddOptions<CosmosDBSettings>()
                .Bind(builder.Configuration.GetSection("CosmosDBSettings"));

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
