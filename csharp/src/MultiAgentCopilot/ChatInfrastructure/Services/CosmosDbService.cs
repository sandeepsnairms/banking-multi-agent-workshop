using BankingAPI.Models.Banking;
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

        public async Task<List<Session>> GetSessionsAsync(string tenantId, string userId)
        {
            try
            {
                QueryDefinition query = new QueryDefinition("SELECT DISTINCT * FROM c WHERE c.type = @type")
                    .WithParameter("@type", nameof(Session));

                FeedIterator<Session> response = _chatData.GetItemQueryIterator<Session>(query);

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
            return await _chatData.ReadItemAsync<Session>(
                id: id,
                partitionKey: new PartitionKey(id));
        }

        public async Task<List<Message>> GetSessionMessagesAsync(string sessionId)
        {
            QueryDefinition query =
                new QueryDefinition("SELECT * FROM c WHERE c.sessionId = @sessionId AND c.type = @type")
                    .WithParameter("@sessionId", sessionId)
                    .WithParameter("@type", nameof(Message));

            FeedIterator<Message> results = _chatData.GetItemQueryIterator<Message>(query);

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
            var response= await _chatData.CreateItemAsync(
                item: session,
                partitionKey: partitionKey
            );

            await UpdateSessionInUserData(OperationType.Add, session.TenantId, session.UserId, session.SessionId, session.Name);

            return response;
        }

        public async Task<Message> InsertMessageAsync(Message message)
        {
            PartitionKey partitionKey = new(message.SessionId);
            return await _chatData.CreateItemAsync(
                item: message,
                partitionKey: partitionKey
            );
        }

        public async Task<Message> UpdateMessageAsync(Message message)
        {
            PartitionKey partitionKey = new(message.SessionId);
            return await _chatData.ReplaceItemAsync(
                item: message,
                id: message.Id,
                partitionKey: partitionKey
            );
        }

        public async Task<Message> UpdateMessageRatingAsync(string messageId, string sessionId, bool? rating)
        {
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
            PartitionKey partitionKey = new(session.SessionId);
            return await _chatData.ReplaceItemAsync(
                item: session,
                id: session.Id,
                partitionKey: partitionKey
            );
        }

        public async Task<Session> UpdateSessionNameAsync(string tenantId, string userId,string sessionId, string name)
        {
            var response = await _chatData.PatchItemAsync<Session>(
                id: sessionId,
                partitionKey: new PartitionKey(sessionId),
                patchOperations: new[]
                {
                        PatchOperation.Set("/name", name),
                }
            );

            UpdateSessionInUserData(OperationType.Rename, tenantId, userId, sessionId, name);

            return response.Resource;
        }

        private enum OperationType
        {
            Add,
            Rename,
            Delete,
        }

        private async Task<bool> UpdateSessionInUserData(OperationType opType, string tenantId, string userId, string sessionId, string? newName = null)
        {
            try
            {
                // Read the document
                var response = await _userData.ReadItemAsync<dynamic>(userId, new PartitionKey(tenantId));
                dynamic document = response.Resource;

                switch (opType)
                {
                    case OperationType.Add:
                        if (document.chatSessions == null || document.chatSessions.Type != JTokenType.Array)
                        {
                            document.chatSessions = new JArray();
                        }
                        document.chatSessions.Add(JObject.FromObject(new { id = sessionId, name = newName }));
                        break;

                    case OperationType.Rename:
                        if (document.chatSessions is JArray chatSessionsArray)
                        {
                            foreach (var session in chatSessionsArray)
                            {
                                if (session["id"]?.ToString() == sessionId)
                                {
                                    session["name"] = newName;  // Correct way to modify JArray
                                    break;
                                }
                            }
                        }
                        break;

                    case OperationType.Delete:
                        if (document.chatSessions is JArray chatSessionsArray2)
                        {
                            var updatedSessions = chatSessionsArray2
                                .Where(session => session["id"]?.ToString() != sessionId)
                                .ToArray();  // Create a new array excluding the session to delete

                            document.chatSessions = new JArray(updatedSessions); // Reassign filtered array
                        }
                        break;
                }

                // Replace the document with updated content
                await _userData.ReplaceItemAsync(document, userId, new PartitionKey(tenantId));
                return true;
            }
            catch (CosmosException ex)
            {
                _logger.LogError(ex.Message);
                return false;
            }
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
            PartitionKey partitionKey = new(sessionId);

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

            await UpdateSessionInUserData(OperationType.Delete, tenantId, userId, sessionId,"");

            await batch.ExecuteAsync();
        }

        public async Task<DebugLog> GetChatCompletionDetailsAsync(string sessionId, string debugLogId)
        {
            return await _chatData.ReadItemAsync<DebugLog>(
                id: debugLogId,
                partitionKey: new PartitionKey(sessionId));

        }
    }
}
