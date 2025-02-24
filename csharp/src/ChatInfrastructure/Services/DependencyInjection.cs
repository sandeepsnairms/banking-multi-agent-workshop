using MultiAgentCopilot.Common.Models.Configuration;
using MultiAgentCopilot.ChatInfrastructure.Interfaces;
using MultiAgentCopilot.ChatInfrastructure.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.ApplicationInsights.AspNetCore.Extensions;
using Microsoft.ApplicationInsights.Extensibility;
using MultiAgentCopilot.ChatInfrastructure.Factories;
using BankingServices.Interfaces;
using BankingServices.Services;
using BankingServices.Models.Configuration;
using Azure.Identity;
namespace MultiAgentCopilot
{
    /// <summary>
    /// General purpose dependency injection extensions.
    /// </summary>
    public static partial class DependencyInjection
    {
        /// <summary>
        /// Registers the <see cref="IBankDBService"/> implementation with the dependency injection container."/>
        /// </summary>
        /// <param name="builder">The hosted applications and services builder.</param>

        /// <summary>
        /// Registers the <see cref="ISemanticKernelService"/> implementation with the dependency injection container.
        /// </summary>
        /// <param name="builder">The hosted applications and services builder.</param>
        public static void AddSemanticKernelService(this IHostApplicationBuilder builder)
        {
            builder.Services.AddOptions<SemanticKernelServiceSettings>()
                .Bind(builder.Configuration.GetSection("SemanticKernelServiceSettings"));
            builder.Services.AddSingleton<ISemanticKernelService, SemanticKernelService>();
        }

        public static void AddCosmosDBService(this IHostApplicationBuilder builder)
        {
            builder.Services.AddOptions<CosmosDBSettings>()
                .Bind(builder.Configuration.GetSection("CosmosDBSettings"));
            builder.Services.AddSingleton<ICosmosDBService, CosmosDBService>();
        }


        public static void AddApplicationInsightsTelemetry(this IHostApplicationBuilder builder)
        {
            // Use Managed Identity to get Application Insights authentication token
            var credential = new DefaultAzureCredential();
            var telemetryConfig = TelemetryConfiguration.CreateDefault();
            telemetryConfig.SetAzureTokenCredential(credential);

            // Add Application Insights telemetry
            builder.Services.AddApplicationInsightsTelemetry(new ApplicationInsightsServiceOptions
            {
                ConnectionString = builder.Configuration["ApplicationInsightsConnectionString"]
            });

            // Register the TelemetryConfiguration instance
            builder.Services.AddSingleton(telemetryConfig);
        }


        /// <summary>
        /// Registers the <see cref="IChatService"/> implementation with the dependency injection container.
        /// </summary>
        /// <param name="builder">The hosted applications and services builder.</param>
        public static void AddChatService(this IHostApplicationBuilder builder)
        {
            builder.Services.AddOptions<BankingCosmosDBSettings>()
                .Bind(builder.Configuration.GetSection("BankingCosmosDBSettings"));
            builder.Services.AddSingleton<IChatService, ChatService>();
        }

    }
}
