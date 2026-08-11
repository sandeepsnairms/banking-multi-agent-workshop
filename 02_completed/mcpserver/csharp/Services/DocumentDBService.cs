using Banking.Services;
using MCPServer.Models.Configuration;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;

namespace MCPServer.Services;

public class DocumentDBService
{
    public IMongoCollection<BsonDocument> AccountDataCollection { get; }
    public IMongoCollection<BsonDocument> TransactionDataCollection { get; }
    public IMongoCollection<BsonDocument> UserDataCollection { get; }
    public IMongoCollection<BsonDocument> OfferDataCollection { get; }
    public IMongoCollection<BsonDocument> RequestDataCollection { get; }
    public IMongoDatabase Database { get; }

    public DocumentDBService(IOptions<DocumentDBSettings> options, ILogger<DocumentDBService> logger)
    {
        DocumentDBSettings settings = options.Value;
        MongoClient client = DocumentDbClientFactory.Create(settings.ClusterName, settings.UserAssignedIdentityClientID);
        Database = client.GetDatabase(settings.Database);
        AccountDataCollection = Database.GetCollection<BsonDocument>(settings.AccountsCollection);
        TransactionDataCollection = Database.GetCollection<BsonDocument>(settings.TransactionsCollection);
        UserDataCollection = Database.GetCollection<BsonDocument>(settings.UserDataCollection);
        OfferDataCollection = Database.GetCollection<BsonDocument>(settings.OfferDataCollection);
        RequestDataCollection = Database.GetCollection<BsonDocument>(settings.RequestDataCollection);
        logger.LogInformation("Azure DocumentDB service initialized for cluster {ClusterName}.", settings.ClusterName);
    }
}