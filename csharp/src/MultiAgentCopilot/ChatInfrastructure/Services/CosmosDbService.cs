using MultiAgentCopilot.Common.Interfaces;
using MultiAgentCopilot.Common.Models.BusinessDomain;
using MultiAgentCopilot.Common.Models.Chat;
using MultiAgentCopilot.Common.Models.Configuration;
using MultiAgentCopilot.ChatInfrastructure.Interfaces;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Fluent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;
using System.Diagnostics;
using Azure.Identity;
using MultiAgentCopilot.Common.Models.Debug;

namespace MultiAgentCopilot.ChatInfrastructure.Services
{
    /// <summary>
    /// Service to access Azure Cosmos DB for NoSQL.
    /// </summary>
    public class CosmosDBService : ICosmosDBService
    {
        private readonly Container _completions;
        private readonly Database _database;
        private readonly ISemanticKernelService _skService;
        private readonly CosmosDBSettings _settings;
        private readonly ILogger _logger;

        public bool IsInitialized { get; private set; }

        public CosmosDBService(
            ISemanticKernelService skService,
            IOptions<CosmosDBSettings> settings,
            ILogger<CosmosDBService> logger)
        {
            _skService = skService;
            _settings = settings.Value;
            ArgumentException.ThrowIfNullOrEmpty(_settings.CosmosUri);

            _logger = logger;
            _logger.LogInformation("Initializing Cosmos DB service.");

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

            CosmosClient client = new CosmosClientBuilder(_settings.CosmosUri, new DefaultAzureCredential())
                .WithSerializerOptions(options)
                .WithConnectionModeGateway()
                .Build();

            _database = client?.GetDatabase(_settings.Database) ??
                        throw new ArgumentException("Unable to connect to existing Azure Cosmos DB database.");

            _completions = _database.GetContainer(_settings.Container.Trim());

            _logger.LogInformation("Cosmos DB service initialized.");
        }

        public async Task<List<Session>> GetSessionsAsync()
        {
            try
            {
                QueryDefinition query = new QueryDefinition("SELECT DISTINCT * FROM c WHERE c.type = @type")
                    .WithParameter("@type", nameof(Session));

                FeedIterator<Session> response = _completions.GetItemQueryIterator<Session>(query);

                List<Session> output = new();
                while (response.HasMoreResults)
                {
                    FeedResponse<Session> results = await response.ReadNextAsync();
                    output.AddRange(results);
                }

                return output;
            }
            catch (CosmosException ex)
            {
                _logger.LogError(ex.Message);
                throw;
            }
        }

        public async Task<Session> GetSessionAsync(string id)
        {
            return await _completions.ReadItemAsync<Session>(
                id: id,
                partitionKey: new PartitionKey(id));
        }

        public async Task<List<Message>> GetSessionMessagesAsync(string sessionId)
        {
            QueryDefinition query =
                new QueryDefinition("SELECT * FROM c WHERE c.sessionId = @sessionId AND c.type = @type")
                    .WithParameter("@sessionId", sessionId)
                    .WithParameter("@type", nameof(Message));

            FeedIterator<Message> results = _completions.GetItemQueryIterator<Message>(query);

            List<Message> output = new();
            while (results.HasMoreResults)
            {
                FeedResponse<Message> response = await results.ReadNextAsync();
                output.AddRange(response);
            }

            return output;
        }

        public async Task<Session> InsertSessionAsync(Session session)
        {
            PartitionKey partitionKey = new(session.SessionId);
            return await _completions.CreateItemAsync(
                item: session,
                partitionKey: partitionKey
            );
        }

        public async Task<Message> InsertMessageAsync(Message message)
        {
            PartitionKey partitionKey = new(message.SessionId);
            return await _completions.CreateItemAsync(
                item: message,
                partitionKey: partitionKey
            );
        }

        public async Task<Message> UpdateMessageAsync(Message message)
        {
            PartitionKey partitionKey = new(message.SessionId);
            return await _completions.ReplaceItemAsync(
                item: message,
                id: message.Id,
                partitionKey: partitionKey
            );
        }

        public async Task<Message> UpdateMessageRatingAsync(string id, string sessionId, bool? rating)
        {
            var response = await _completions.PatchItemAsync<Message>(
                id: id,
                partitionKey: new PartitionKey(sessionId),
                patchOperations: new[]
                {
                        PatchOperation.Set("/rating", rating),
                }
            );
            return response.Resource;
        }

        public async Task<Session> UpdateSessionAsync(Session session)
        {
            PartitionKey partitionKey = new(session.SessionId);
            return await _completions.ReplaceItemAsync(
                item: session,
                id: session.Id,
                partitionKey: partitionKey
            );
        }

        public async Task<Session> UpdateSessionNameAsync(string id, string name)
        {
            var response = await _completions.PatchItemAsync<Session>(
                id: id,
                partitionKey: new PartitionKey(id),
                patchOperations: new[]
                {
                        PatchOperation.Set("/name", name),
                }
            );
            return response.Resource;
        }

        public async Task UpsertSessionBatchAsync(List<Message> messages, List<DebugLog>debugLogs, Session session)
        {
            if (messages.Select(m => m.SessionId).Distinct().Count() > 1 || session.SessionId != messages.Select(m => m.SessionId).FirstOrDefault())
            {
                throw new ArgumentException("All items must have the same partition key.");
            }

            if (debugLogs.Select(m => m.SessionId).Distinct().Count() > 1 || session.SessionId != debugLogs.Select(m => m.SessionId).FirstOrDefault())
            {
                throw new ArgumentException("All items must have the same partition key as message.");
            }

            PartitionKey partitionKey = new(messages.First().SessionId);
            var batch = _completions.CreateTransactionalBatch(partitionKey);
            foreach (var message in messages)
            {
                batch.UpsertItem(
                    item: message
                );
            }

            foreach (var log in debugLogs)
            {
                batch.UpsertItem(
                    item: log
                );
            }

            batch.UpsertItem(
                item: session
            );

            await batch.ExecuteAsync();
        }

        public async Task DeleteSessionAndMessagesAsync(string sessionId)
        {
            PartitionKey partitionKey = new(sessionId);

            var query = new QueryDefinition("SELECT c.id FROM c WHERE c.sessionId = @sessionId")
                .WithParameter("@sessionId", sessionId);

            var response = _completions.GetItemQueryIterator<Message>(query);

            var batch = _completions.CreateTransactionalBatch(partitionKey);
            while (response.HasMoreResults)
            {
                var results = await response.ReadNextAsync();
                foreach (var item in results)
                {
                    batch.DeleteItem(
                        id: item.Id
                    );
                }
            }

            await batch.ExecuteAsync();
        }

        public async Task<DebugLog> GetChatCompletionDetailsAsync(string sessionId, string debugLogId)
        {
            return await _completions.ReadItemAsync<DebugLog>(
                id: debugLogId,
                partitionKey: new PartitionKey(sessionId));

        }
    }
}
