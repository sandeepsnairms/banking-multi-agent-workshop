using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;

string? clusterName = args.FirstOrDefault() ?? Environment.GetEnvironmentVariable("DOCUMENTDB_CLUSTER_NAME");
if (string.IsNullOrWhiteSpace(clusterName))
{
    throw new InvalidOperationException("Pass the Azure DocumentDB cluster name as the first argument or set DOCUMENTDB_CLUSTER_NAME.");
}

string databaseName = Environment.GetEnvironmentVariable("DOCUMENTDB_DATABASE_NAME") ?? "MultiAgentBanking";
string? managedIdentityClientId = Environment.GetEnvironmentVariable("AZURE_CLIENT_ID");
MongoClient client = DocumentDbClientFactory.Create(clusterName, managedIdentityClientId);
IMongoDatabase database = client.GetDatabase(databaseName);

string[] collectionNames = ["Users", "OffersData", "AccountsData", "ChatsData"];
HashSet<string> existing = (await database.ListCollectionNamesAsync()).ToList().ToHashSet(StringComparer.Ordinal);
foreach (string collectionName in collectionNames.Where(name => !existing.Contains(name)))
{
    await database.CreateCollectionAsync(collectionName);
}

await CreateIndexesAsync(database);
await UpsertFileAsync(database.GetCollection<BsonDocument>("AccountsData"), "AccountsData.json");
await UpsertFileAsync(database.GetCollection<BsonDocument>("OffersData"), "OffersData.json");
await UpsertFileAsync(database.GetCollection<BsonDocument>("Users"), "UserData.json");
Console.WriteLine("Azure DocumentDB data loading complete.");

static async Task UpsertFileAsync(IMongoCollection<BsonDocument> collection, string fileName)
{
    string json = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, fileName));
    BsonArray items = BsonSerializer.Deserialize<BsonArray>(json);
    List<WriteModel<BsonDocument>> writes = [];
    foreach (BsonDocument document in items.Select(item => item.AsBsonDocument))
    {
        if (!document.TryGetValue("id", out BsonValue? id) || !id.IsString)
        {
            throw new InvalidDataException($"{fileName} contains a document without a string id.");
        }
        document["_id"] = id;
        writes.Add(new ReplaceOneModel<BsonDocument>(Builders<BsonDocument>.Filter.Eq("id", id), document) { IsUpsert = true });
    }
    if (writes.Count > 0)
    {
        await collection.BulkWriteAsync(writes, new BulkWriteOptions { IsOrdered = true });
    }
    Console.WriteLine($"Upserted {writes.Count} documents from {fileName}.");
}

static async Task CreateIndexesAsync(IMongoDatabase database)
{
    IMongoCollection<BsonDocument> accounts = database.GetCollection<BsonDocument>("AccountsData");
    await accounts.Indexes.CreateManyAsync(
    [
        new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending("tenantId").Ascending("userId").Ascending("type")),
        new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending("tenantId").Ascending("accountId").Ascending("transactionDateTime"))
    ]);
    await database.GetCollection<BsonDocument>("Users").Indexes.CreateOneAsync(
        new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending("tenantId").Ascending("id")));

    IMongoCollection<BsonDocument> offers = database.GetCollection<BsonDocument>("OffersData");
    await offers.Indexes.CreateManyAsync(
    [
        new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending("tenantId")),
        new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending("type")),
        new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending("accountType"))
    ]);

    BsonDocument command = new()
    {
        { "createIndexes", "OffersData" },
        { "indexes", new BsonArray
            {
                new BsonDocument
                {
                    { "name", "offers-vector-ivf" },
                    { "key", new BsonDocument("vector", "cosmosSearch") },
                    { "cosmosSearchOptions", new BsonDocument
                        {
                            { "kind", "vector-ivf" },
                            { "numLists", 1 },
                            { "similarity", "COS" },
                            { "dimensions", 1536 }
                        }
                    }
                }
            }
        }
    };
    await database.RunCommandAsync<BsonDocument>(command);
}