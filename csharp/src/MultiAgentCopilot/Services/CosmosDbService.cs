using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Fluent;

using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;
using System.Diagnostics;
using Azure.Identity;
using System.Text.Json;

using PartitionKey = Microsoft.Azure.Cosmos.PartitionKey;
using Container = Microsoft.Azure.Cosmos.Container;
using MultiAgentCopilot.Helper;
using MultiAgentCopilot.Models.Chat;
using MultiAgentCopilot.Models.Configuration;
using MultiAgentCopilot.Models.Debug;

using Message =  MultiAgentCopilot.Models.Chat.Message;

namespace MultiAgentCopilot.Services
{
    /// <summary>
    /// Service to access Azure Cosmos DB for NoSQL.
    /// </summary>
    public class CosmosDBService 
    {
        public  Container ChatDataContainer { get; }
        public Container UserDataContainer { get; }
        public Container OfferDataContainer { get; }
        public Container AccountDataContainer { get; }

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

            ChatDataContainer = Database.GetContainer(_settings.ChatDataContainer.Trim());
            UserDataContainer = Database.GetContainer(_settings.UserDataContainer.Trim());
            OfferDataContainer = Database.GetContainer(_settings.OfferDataContainer.Trim());
            AccountDataContainer = Database.GetContainer(_settings.AccountsContainer.Trim());

            _logger.LogInformation("Cosmos DB service initialized.");
        }

        public async Task<List<Session>> GetUserSessionsAsync(string tenantId, string userId)
        {
            try
            {
                QueryDefinition query = new QueryDefinition("SELECT DISTINCT * FROM c WHERE c.type = @type")
                    .WithParameter("@type", nameof(Session));

                var partitionKey= PartitionManager.GetChatDataPartialPK(tenantId, userId);
                FeedIterator<Session> response = ChatDataContainer.GetItemQueryIterator<Session>(query, null, new QueryRequestOptions() { PartitionKey = partitionKey });

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
                _logger.LogError(ex.ToString());
                throw;
            }
        }

        public async Task<Session> GetSessionAsync(string tenantId, string userId, string sessionId)
        {
            try
            { 
                var partitionKey = PartitionManager.GetChatDataFullPK(tenantId, userId,sessionId);

                return await ChatDataContainer.ReadItemAsync<Session>(
                    id: sessionId,
                    partitionKey: partitionKey);
            }
            catch (CosmosException ex)
            {
                _logger.LogError(ex.ToString());
                throw;
            }
        }

        public async Task<List<Message>> GetSessionMessagesAsync(string tenantId, string userId,string sessionId)
        {
            try
            {
                QueryDefinition query =
                    new QueryDefinition("SELECT * FROM c WHERE c.type = @type")
                        .WithParameter("@type", nameof(Message));

                var partitionKey = PartitionManager.GetChatDataFullPK(tenantId, userId, sessionId);

                FeedIterator<Message> results = ChatDataContainer.GetItemQueryIterator<Message>(query, null, new QueryRequestOptions() { PartitionKey = partitionKey });

                List<Message> output = new();
                while (results.HasMoreResults)
                {
                    FeedResponse<Message> response = await results.ReadNextAsync();
                    output.AddRange(response);
                }

                return output;
            }
            catch (CosmosException ex)
            {
                _logger.LogError(ex.ToString());
                throw;
            }
}

        public async Task<Session> InsertSessionAsync(Session session)
        {
            try
            { 
                var partitionKey = PartitionManager.GetChatDataFullPK(session.TenantId, session.UserId,session.SessionId);

                var response= await ChatDataContainer.CreateItemAsync(
                    item: session,
                    partitionKey: partitionKey
                );

                return response;
            }
            catch (CosmosException ex)
            {
                _logger.LogError(ex.ToString());
                throw;
            }
        }

        public async Task<Message> InsertMessageAsync(Message message)
        {
            try
            { 
                var partitionKey = PartitionManager.GetChatDataFullPK(message.TenantId, message.UserId, message.SessionId);

                return await ChatDataContainer.CreateItemAsync(
                    item: message,
                    partitionKey: partitionKey
                );
            }
            catch (CosmosException ex)
            {
                _logger.LogError(ex.ToString());
                throw;
            }
        }

        public async Task<Message> UpdateMessageAsync(Message message)
        {
            try
            { 
                var partitionKey = PartitionManager.GetChatDataFullPK(message.TenantId, message.UserId, message.SessionId);

                return await ChatDataContainer.ReplaceItemAsync(
                    item: message,
                    id: message.Id,
                    partitionKey: partitionKey
                );
            }
            catch (CosmosException ex)
            {
                _logger.LogError(ex.ToString());
                throw;
            }
        }

        public async Task<Message> UpdateMessageRatingAsync(string tenantId, string userId, string sessionId,string messageId, bool? rating)
        {
            try
            { 
                var partitionKey = PartitionManager.GetChatDataFullPK(tenantId, userId, sessionId);

                var response = await ChatDataContainer.PatchItemAsync<Message>(
                id: messageId,
                partitionKey: partitionKey,
                    patchOperations: new[]
                    {
                            PatchOperation.Set("/rating", rating),
                    }
                );
                return response.Resource;
            }
            catch (CosmosException ex)
            {
                _logger.LogError(ex.ToString());
                throw;
            }
        }

        public async Task<Session> UpdateSessionAsync(Session session)
        {
            try
            { 
                var partitionKey = PartitionManager.GetChatDataFullPK(session.TenantId, session.UserId, session.SessionId);

                return await ChatDataContainer.ReplaceItemAsync(
                    item: session,
                    id: session.Id,
                    partitionKey: partitionKey
                );
            }
            catch (CosmosException ex)
            {
                _logger.LogError(ex.ToString());
                throw;
            }
        }

        public async Task<Session> UpdateSessionNameAsync(string tenantId, string userId,string sessionId, string name)
        {
            try
            {
                var partitionKey = PartitionManager.GetChatDataFullPK(tenantId, userId, sessionId);

                var response = await ChatDataContainer.PatchItemAsync<Session>(
                    id: sessionId,
                    partitionKey: partitionKey,
                    patchOperations: new[]
                    {
                        PatchOperation.Set("/name", name),
                    }
                );


                return response.Resource;
            }
            catch (CosmosException ex)
            {
                _logger.LogError(ex.ToString());
                throw;
            }
}



        public async Task UpsertSessionBatchAsync(List<Message> messages, List<DebugLog>debugLogs, Session session)
        {
            try
            { 
                if (messages.Select(m => m.SessionId).Distinct().Count() > 1 || session.SessionId != messages.Select(m => m.SessionId).FirstOrDefault())
                {
                    throw new ArgumentException("All items must have the same partition key.");
                }

                if (debugLogs.Count > 0 && (debugLogs.Select(m => m.SessionId).Distinct().Count() > 1 || session.SessionId != debugLogs.Select(m => m.SessionId).FirstOrDefault()))
                {
                    throw new ArgumentException("All items must have the same partition key as message.");
                }

                PartitionKey partitionKey = PartitionManager.GetChatDataFullPK(session.TenantId, session.UserId, session.SessionId);
                var batch = ChatDataContainer.CreateTransactionalBatch(partitionKey);
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
            catch (CosmosException ex)
            {
                _logger.LogError(ex.ToString());
                throw;
            }
        }

        public async Task DeleteSessionAndMessagesAsync(string tenantId, string userId,string sessionId)
        {
            try
            { 
                var partitionKey = PartitionManager.GetChatDataFullPK(tenantId, userId, sessionId);

                var query = new QueryDefinition("SELECT c.id FROM c WHERE c.sessionId = @sessionId")
                    .WithParameter("@sessionId", sessionId);

                var response = ChatDataContainer.GetItemQueryIterator<Message>(query);

                var batch = ChatDataContainer.CreateTransactionalBatch(partitionKey);
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
            catch (CosmosException ex)
            {
                _logger.LogError(ex.ToString());
                throw;
            }
        }

        public async Task<DebugLog> InsertDebugLogAsync(DebugLog debugLog)
        {
            try
            { 
                var partitionKey = PartitionManager.GetChatDataFullPK(debugLog.TenantId, debugLog.UserId, debugLog.SessionId);

                return await ChatDataContainer.CreateItemAsync(
                    item: debugLog,
                    partitionKey: partitionKey
                );
            }
            catch (CosmosException ex)
            {
                _logger.LogError(ex.ToString());
                throw;
            }
        }

        public async Task<DebugLog> GetChatCompletionDebugLogAsync(string tenantId, string userId,string sessionId, string debugLogId)
        {
            try
            { 
                var partitionKey = PartitionManager.GetChatDataFullPK(tenantId, userId, sessionId);

                return await ChatDataContainer.ReadItemAsync<DebugLog>(
                    id: debugLogId,
                    partitionKey: partitionKey);
            }
            catch (CosmosException ex)
            {
                _logger.LogError(ex.ToString());
                throw;
            }
        }
        

        public async Task<bool> InsertDocumentAsync(string containerName, JObject document)
        {
            Container container=null;

            switch (containerName)
            {
                case "OfferData":
                    container = OfferDataContainer;
                    break;
                case "AccountData":
                    container = AccountDataContainer;
                    break;
                case "UserData":
                    container = UserDataContainer;
                    break;
            }
            try
            {

                // Insert cleaned document
                await container.CreateItemAsync(document);


                return true;
            }
            catch (CosmosException ex)
            {
                // Ignore conflict errors.
                if (ex.StatusCode == System.Net.HttpStatusCode.Conflict)
                {
                    _logger.LogInformation($"Duplicate document detected.");
                }
                else
                {
                    _logger.LogError(ex, "Error inserting document.");
                    throw;
                }
                return false;
            }
        }

    }
}
