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

        public static void AddDocumentDBService(this IHostApplicationBuilder builder)
        {
            builder.Services.AddOptions<DocumentDBSettings>()
                .Bind(builder.Configuration.GetSection("DocumentDBSettings"));

            builder.Services.AddSingleton<DocumentDBService>();
        }

        public static void AddChatService(this IHostApplicationBuilder builder)
        {
            builder.Services.AddSingleton<ChatService>();
        }

    }
}
