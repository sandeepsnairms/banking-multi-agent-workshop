using Azure.Identity;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Fluent;
using Microsoft.Extensions.Options;
using MCPServer.Models.Configuration;
using System.Diagnostics;
using System.Text.Json;
using Container = Microsoft.Azure.Cosmos.Container;

namespace MCPServer.Services
{
    /// <summary>
    /// Service to access Azure Cosmos DB for NoSQL.
    /// </summary>
    public class CosmosDBService
    {
        public Container AccountDataContainer { get; }
        public Container UserDataContainer { get; }
        public Container OfferDataContainer { get; }
        public Container RequestDataContainer { get; }

        public Database Database { get; }

        private readonly CosmosDBSettings _settings;
        private readonly ILogger _logger;

        public CosmosDBService(
            IOptions<CosmosDBSettings> settings,
            ILogger<CosmosDBService> logger)
        {
            _settings = settings.Value;
            ArgumentException.ThrowIfNullOrEmpty(_settings.CosmosUri);

            _logger = logger;
            _logger.LogInformation("Initializing Cosmos DB service for MCP Server.");

            if (!_settings.EnableTracing)
            {
                Type defaultTrace = Type.GetType("Microsoft.Azure.Cosmos.Core.Trace.DefaultTrace,Microsoft.Azure.Cosmos.Direct");
                TraceSource traceSource = (TraceSource)defaultTrace.GetProperty("TraceSource").GetValue(null);
                traceSource.Switch.Level = SourceLevels.All;
                traceSource.Listeners.Clear();
            }

            CosmosSerializationOptions options = new()
            {
                PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase
            };

            DefaultAzureCredential credential;
            if (string.IsNullOrEmpty(_settings.UserAssignedIdentityClientID))
            {
                credential = new DefaultAzureCredential();
            }
            else
            {
                credential = new DefaultAzureCredential(new DefaultAzureCredentialOptions
                {
                    ManagedIdentityClientId = _settings.UserAssignedIdentityClientID
                });
            }

            var jsonSerializerOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

            CosmosClient client = new CosmosClientBuilder(_settings.CosmosUri, credential)
                .WithSystemTextJsonSerializerOptions(jsonSerializerOptions)
                .WithConnectionModeGateway()
                .Build();

            Database = client?.GetDatabase(_settings.Database) ??
                        throw new ArgumentException("Unable to connect to existing Azure Cosmos DB database.");

            AccountDataContainer = Database.GetContainer(_settings.AccountsContainer.Trim());
            UserDataContainer = Database.GetContainer(_settings.UserDataContainer.Trim());
            OfferDataContainer = Database.GetContainer(_settings.OfferDataContainer.Trim());
            RequestDataContainer = Database.GetContainer(_settings.RequestDataContainer.Trim());

            _logger.LogInformation("Cosmos DB service initialized for MCP Server.");
        }
    }
}