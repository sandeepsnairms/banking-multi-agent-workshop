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
using System.Security.Principal;
using Newtonsoft.Json;
using System.Text.Json;
namespace MultiAgentCopilot.ChatInfrastructure.Services
{
    /// <summary>
    /// Service to access Azure Cosmos DB for NoSQL.
    /// </summary>
    public class CosmosDBService : ICosmosDBService
    {
        private readonly Container _chatData;
        private readonly Container _userData;
        private readonly Container _offersData;
        private readonly Container _accountsData;

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
            CosmosClient client = new CosmosClientBuilder(_settings.CosmosUri, credential)
                .WithSerializerOptions(options)
                .WithConnectionModeGateway()
                .Build();

            _database = client?.GetDatabase(_settings.Database) ??
                        throw new ArgumentException("Unable to connect to existing Azure Cosmos DB database.");

            _chatData = _database.GetContainer(_settings.ChatDataContainer.Trim());
            _userData = _database.GetContainer(_settings.UserDataContainer.Trim());
            _offersData = _database.GetContainer(_settings.OfferDataContainer.Trim());
            _accountsData = _database.GetContainer(_settings.AccountsContainer.Trim());

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
                _logger.LogError(ex.ToString());
                throw;
            }
        }

        public async Task<Session> GetSessionAsync(string tenantId, string userId, string sessionId)
        {
            try
            { 
                var partitionKey = PartitionManager.GetChatDataFullPK(tenantId, userId,sessionId);

                return await _chatData.ReadItemAsync<Session>(
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

                FeedIterator<Message> results = _chatData.GetItemQueryIterator<Message>(query, null, new QueryRequestOptions() { PartitionKey = partitionKey });

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

                var response= await _chatData.CreateItemAsync(
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

                return await _chatData.CreateItemAsync(
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

                return await _chatData.ReplaceItemAsync(
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

                var response = await _chatData.PatchItemAsync<Message>(
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

                return await _chatData.ReplaceItemAsync(
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

                return await _chatData.ReadItemAsync<DebugLog>(
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
                    container = _offersData;
                    break;
                case "AccountData":
                    container = _accountsData;
                    break;
                case "UserData":
                    container = _userData;
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
