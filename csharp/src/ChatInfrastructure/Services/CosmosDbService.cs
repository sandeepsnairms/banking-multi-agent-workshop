using MultiAgentCopilot.Common.Models.Banking;
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
using System.ComponentModel;
using Microsoft.Azure.Cosmos.Serialization.HybridRow.Schemas;
using System.Xml.Linq;
using PartitionKey = Microsoft.Azure.Cosmos.PartitionKey;
using Container = Microsoft.Azure.Cosmos.Container;
using MultiAgentCopilot.Common.Helper;
using System.Collections.Concurrent;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.DataCollection;
using Microsoft.VisualStudio.TestPlatform.CommunicationUtilities;
using Message = MultiAgentCopilot.Common.Models.Chat.Message;
namespace MultiAgentCopilot.ChatInfrastructure.Services
{
    /// <summary>
    /// Service to access Azure Cosmos DB for NoSQL.
    /// </summary>
    public class CosmosDBService : ICosmosDBService
    {
        private readonly Container _chatData;
        private readonly Container _userData;

        private readonly Database _database;
        private readonly CosmosDBSettings _settings;
        private readonly ILogger _logger;


        public CosmosDBService(
            IOptions<CosmosDBSettings> settings,
            ILogger<CosmosDBService> logger)
        {
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

            _chatData = _database.GetContainer(_settings.ChatDataContainer.Trim());
            _userData = _database.GetContainer(_settings.UserDataContainer.Trim());

            _logger.LogInformation("Cosmos DB service initialized.");
        }

        public async Task<List<Session>> GetUserSessionsAsync(string tenantId, string userId)
        {
            try
            {
                QueryDefinition query = new QueryDefinition("SELECT DISTINCT * FROM c WHERE c.type = @type")
                    .WithParameter("@type", nameof(Session));

                var partitionKey= PartitionManager.GetChatDataPartialPK(tenantId, userId);
                FeedIterator<Session> response = _chatData.GetItemQueryIterator<Session>(query, null, new QueryRequestOptions() { PartitionKey = partitionKey });

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

        public async Task<Session> GetSessionAsync(string tenantId, string userId, string sessionId)
        {
            var partitionKey = PartitionManager.GetChatDataFullPK(tenantId, userId,sessionId);

            return await _chatData.ReadItemAsync<Session>(
                id: sessionId,
                partitionKey: partitionKey);

        }

        public async Task<List<Message>> GetSessionMessagesAsync(string tenantId, string userId,string sessionId)
        {
            QueryDefinition query =
                new QueryDefinition("SELECT * FROM c WHERE c.type = @type")
                    .WithParameter("@type", nameof(Message));

            var partitionKey = PartitionManager.GetChatDataFullPK(tenantId, userId,sessionId);

            FeedIterator<Message> results = _chatData.GetItemQueryIterator<Message>(query, null, new QueryRequestOptions() { PartitionKey = partitionKey });

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
            var partitionKey = PartitionManager.GetChatDataFullPK(session.TenantId, session.UserId,session.SessionId);

            var response= await _chatData.CreateItemAsync(
                item: session,
                partitionKey: partitionKey
            );

            return response;
        }

        public async Task<Message> InsertMessageAsync(Message message)
        {
            var partitionKey = PartitionManager.GetChatDataFullPK(message.TenantId, message.UserId, message.SessionId);

            return await _chatData.CreateItemAsync(
                item: message,
                partitionKey: partitionKey
            );
        }

        public async Task<Message> UpdateMessageAsync(Message message)
        {
            var partitionKey = PartitionManager.GetChatDataFullPK(message.TenantId, message.UserId, message.SessionId);

            return await _chatData.ReplaceItemAsync(
                item: message,
                id: message.Id,
                partitionKey: partitionKey
            );
        }

        public async Task<Message> UpdateMessageRatingAsync(string tenantId, string userId, string sessionId,string messageId, bool? rating)
        {
            var partitionKey = PartitionManager.GetChatDataFullPK(tenantId, userId, sessionId);

            var response = await _chatData.PatchItemAsync<Message>(
            id: messageId,
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
            var partitionKey = PartitionManager.GetChatDataFullPK(session.TenantId, session.UserId, session.SessionId);

            return await _chatData.ReplaceItemAsync(
                item: session,
                id: session.Id,
                partitionKey: partitionKey
            );
        }

        public async Task<Session> UpdateSessionNameAsync(string tenantId, string userId,string sessionId, string name)
        {
            var partitionKey = PartitionManager.GetChatDataFullPK(tenantId, userId, sessionId);

            var response = await _chatData.PatchItemAsync<Session>(
                id: sessionId,
                partitionKey: partitionKey,
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

            PartitionKey partitionKey = PartitionManager.GetChatDataFullPK(session.TenantId, session.UserId, session.SessionId);
            var batch = _chatData.CreateTransactionalBatch(partitionKey);
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

        public async Task DeleteSessionAndMessagesAsync(string tenantId, string userId,string sessionId)
        {
            var partitionKey = PartitionManager.GetChatDataFullPK(tenantId, userId, sessionId);

            var query = new QueryDefinition("SELECT c.id FROM c WHERE c.sessionId = @sessionId")
                .WithParameter("@sessionId", sessionId);

            var response = _chatData.GetItemQueryIterator<Message>(query);

            var batch = _chatData.CreateTransactionalBatch(partitionKey);
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

        public async Task<DebugLog> GetChatCompletionDebugLogAsync(string tenantId, string userId,string sessionId, string debugLogId)
        {
            var partitionKey = PartitionManager.GetChatDataFullPK(tenantId, userId, sessionId);

            return await _chatData.ReadItemAsync<DebugLog>(
                id: debugLogId,
                partitionKey: partitionKey);

        }
    }
}
