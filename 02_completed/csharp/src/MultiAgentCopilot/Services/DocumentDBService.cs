using Banking.Models;
using Banking.Services;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using MultiAgentCopilot.Models.Chat;
using MultiAgentCopilot.Models.Configuration;
using MultiAgentCopilot.Models.Debug;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Text.Json;
using Message = MultiAgentCopilot.Models.Chat.Message;

namespace MultiAgentCopilot.Services;

public class DocumentDBService
{
    public IMongoCollection<BsonDocument> ChatDataCollection { get; }
    public IMongoCollection<BsonDocument> UserDataCollection { get; }
    public IMongoCollection<BsonDocument> OfferDataCollection { get; }
    public IMongoCollection<BsonDocument> AccountDataCollection { get; }
    public IMongoCollection<BsonDocument> RequestDataCollection { get; }
    public IMongoDatabase Database { get; }

    private readonly ILogger<DocumentDBService> _logger;

    public DocumentDBService(IOptions<DocumentDBSettings> options, ILogger<DocumentDBService> logger)
    {
        DocumentDBSettings settings = options.Value;
        _logger = logger;
        MongoClient client = DocumentDbClientFactory.Create(settings.ClusterName, settings.UserAssignedIdentityClientID);
        Database = client.GetDatabase(settings.Database);
        ChatDataCollection = Database.GetCollection<BsonDocument>(settings.ChatDataCollection);
        UserDataCollection = Database.GetCollection<BsonDocument>(settings.UserDataCollection);
        OfferDataCollection = Database.GetCollection<BsonDocument>(settings.OfferDataCollection);
        AccountDataCollection = Database.GetCollection<BsonDocument>(settings.AccountsCollection);
        RequestDataCollection = Database.GetCollection<BsonDocument>(settings.RequestDataCollection);
        _logger.LogInformation("Azure DocumentDB service initialized for cluster {ClusterName}.", settings.ClusterName);
    }

    public async Task<List<Session>> GetUserSessionsAsync(string tenantId, string userId)
    {
        FilterDefinition<BsonDocument> filter = OwnerFilter(tenantId, userId) &
            Builders<BsonDocument>.Filter.Eq("type", nameof(Session));
        return Convert<Session>(await ChatDataCollection.Find(filter).ToListAsync());
    }

    public async Task<Session> GetSessionAsync(string tenantId, string userId, string sessionId) =>
        DocumentDbSerialization.FromDocument<Session>(await FindRequiredAsync<Session>(tenantId, userId, sessionId));

    public async Task<List<Message>> GetSessionMessagesAsync(string tenantId, string userId, string sessionId)
    {
        FilterDefinition<BsonDocument> filter = SessionFilter(tenantId, userId, sessionId) &
            Builders<BsonDocument>.Filter.Eq("type", nameof(Message));
        return Convert<Message>(await ChatDataCollection.Find(filter).Sort(Builders<BsonDocument>.Sort.Ascending("timeStamp")).ToListAsync());
    }

    public Task<Session> InsertSessionAsync(Session session) => InsertAsync(session);
    public Task<Message> InsertMessageAsync(Message message) => InsertAsync(message);
    public Task<Message> UpdateMessageAsync(Message message) => ReplaceAsync(message, message.TenantId, message.UserId, message.SessionId);
    public Task<Session> UpdateSessionAsync(Session session) => ReplaceAsync(session, session.TenantId, session.UserId, session.SessionId);

    public async Task<Message> UpdateMessageRatingAsync(string tenantId, string userId, string sessionId, string messageId, bool? rating) =>
        DocumentDbSerialization.FromDocument<Message>(await UpdateRequiredAsync(
            SessionFilter(tenantId, userId, sessionId) & Builders<BsonDocument>.Filter.Eq("id", messageId),
            Builders<BsonDocument>.Update.Set("rating", rating)));

    public async Task<Session> UpdateSessionNameAsync(string tenantId, string userId, string sessionId, string name) =>
        DocumentDbSerialization.FromDocument<Session>(await UpdateRequiredAsync(
            SessionFilter(tenantId, userId, sessionId) & Builders<BsonDocument>.Filter.Eq("type", nameof(Session)),
            Builders<BsonDocument>.Update.Set("name", name)));

    public async Task UpsertSessionBatchAsync(List<Message> messages, List<DebugLog> debugLogs, Session session)
    {
        if (messages.Any(message => message.SessionId != session.SessionId) ||
            debugLogs.Any(log => log.SessionId != session.SessionId))
        {
            throw new ArgumentException("All items must belong to the supplied session.");
        }

        List<WriteModel<BsonDocument>> writes = [];
        writes.AddRange(messages.Select(Upsert));
        writes.AddRange(debugLogs.Select(Upsert));
        writes.Add(Upsert(session));
        await ChatDataCollection.BulkWriteAsync(writes, new BulkWriteOptions { IsOrdered = true });
    }

    public Task DeleteSessionAndMessagesAsync(string tenantId, string userId, string sessionId) =>
        ChatDataCollection.DeleteManyAsync(SessionFilter(tenantId, userId, sessionId));

    public async Task<DebugLog> GetChatCompletionDebugLogAsync(string tenantId, string userId, string sessionId, string debugLogId) =>
        DocumentDbSerialization.FromDocument<DebugLog>(await FindRequiredAsync<DebugLog>(tenantId, userId, sessionId, debugLogId));

    public async Task<bool> AddDocument(string collectionName, JsonElement document)
    {
        try
        {
            BsonDocument bson = BsonDocument.Parse(document.GetRawText());
            DocumentDbSerialization.EnsureMongoId(bson);
            IMongoCollection<BsonDocument> collection = collectionName switch
            {
                "OfferData" => OfferDataCollection,
                "AccountData" => AccountDataCollection,
                "UserData" => UserDataCollection,
                _ => throw new ArgumentOutOfRangeException(nameof(collectionName))
            };
            ReplaceOneResult result = await collection.ReplaceOneAsync(
                Builders<BsonDocument>.Filter.Eq("id", bson["id"]), bson, new ReplaceOptions { IsUpsert = true });
            return result.IsAcknowledged;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding document to collection {CollectionName}.", collectionName);
            return false;
        }
    }

    public async Task<List<ServiceRequest>> GetServiceRequestsAsync(string tenantId)
    {
        FilterDefinition<BsonDocument> filter = Builders<BsonDocument>.Filter.Eq("tenantId", tenantId) &
            Builders<BsonDocument>.Filter.Eq("type", nameof(ServiceRequest));
        return Convert<ServiceRequest>(await RequestDataCollection.Find(filter)
            .Sort(Builders<BsonDocument>.Sort.Descending("requestedOn")).Limit(10).ToListAsync());
    }

    public async Task<List<BankAccount>> GetUserRegisteredAccountsAsync(string tenantId, string userId)
    {
        FilterDefinition<BsonDocument> filter = OwnerFilter(tenantId, userId) &
            Builders<BsonDocument>.Filter.Eq("type", nameof(BankAccount));
        return Convert<BankAccount>(await AccountDataCollection.Find(filter).ToListAsync());
    }

    public async Task<List<BankTransaction>> GetAccountTransactionsAsync(string tenantId, string userId, string accountId)
    {
        _ = userId;
        FilterDefinition<BsonDocument> filter = Builders<BsonDocument>.Filter.Eq("tenantId", tenantId) &
            Builders<BsonDocument>.Filter.Eq("accountId", accountId) &
            Builders<BsonDocument>.Filter.Eq("type", nameof(BankTransaction));
        return Convert<BankTransaction>(await AccountDataCollection.Find(filter)
            .Sort(Builders<BsonDocument>.Sort.Ascending("transactionDateTime")).Limit(10).ToListAsync());
    }

    private async Task<T> InsertAsync<T>(T value)
    {
        await ChatDataCollection.InsertOneAsync(DocumentDbSerialization.ToDocument(value));
        return value;
    }

    private async Task<T> ReplaceAsync<T>(T value, string tenantId, string userId, string sessionId)
    {
        BsonDocument document = DocumentDbSerialization.ToDocument(value);
        ReplaceOneResult result = await ChatDataCollection.ReplaceOneAsync(
            SessionFilter(tenantId, userId, sessionId) & Builders<BsonDocument>.Filter.Eq("id", document["id"]), document);
        if (result.MatchedCount == 0) throw new KeyNotFoundException(document["id"].AsString);
        return value;
    }

    private async Task<BsonDocument> FindRequiredAsync<T>(string tenantId, string userId, string sessionId, string? id = null)
    {
        FilterDefinition<BsonDocument> filter = SessionFilter(tenantId, userId, sessionId) &
            Builders<BsonDocument>.Filter.Eq("type", typeof(T).Name);
        if (id is not null) filter &= Builders<BsonDocument>.Filter.Eq("id", id);
        return await ChatDataCollection.Find(filter).FirstOrDefaultAsync()
            ?? throw new KeyNotFoundException(id ?? sessionId);
    }

    private async Task<BsonDocument> UpdateRequiredAsync(FilterDefinition<BsonDocument> filter, UpdateDefinition<BsonDocument> update) =>
        await ChatDataCollection.FindOneAndUpdateAsync(filter, update, new FindOneAndUpdateOptions<BsonDocument>
        {
            ReturnDocument = ReturnDocument.After
        }) ?? throw new KeyNotFoundException("Document was not found.");

    private static ReplaceOneModel<BsonDocument> Upsert<T>(T value)
    {
        BsonDocument document = DocumentDbSerialization.ToDocument(value);
        return new ReplaceOneModel<BsonDocument>(Builders<BsonDocument>.Filter.Eq("id", document["id"]), document) { IsUpsert = true };
    }

    private static FilterDefinition<BsonDocument> OwnerFilter(string tenantId, string userId) =>
        Builders<BsonDocument>.Filter.Eq("tenantId", tenantId) & Builders<BsonDocument>.Filter.Eq("userId", userId);

    private static FilterDefinition<BsonDocument> SessionFilter(string tenantId, string userId, string sessionId) =>
        OwnerFilter(tenantId, userId) & Builders<BsonDocument>.Filter.Eq("sessionId", sessionId);

    private static List<T> Convert<T>(IEnumerable<BsonDocument> documents) =>
        documents.Select(DocumentDbSerialization.FromDocument<T>).ToList();
}