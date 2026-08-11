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

string[] collectionNames =
[
    "Users",
    "Accounts",
    "Transactions",
    "ServiceRequests",
    "Offers",
    "ChatsData",
    "Checkpoints",
    "CheckpointWrites",
    "ChatHistory",
    "Debug"
];
HashSet<string> existing = (await database.ListCollectionNamesAsync()).ToList().ToHashSet(StringComparer.Ordinal);
foreach (string collectionName in collectionNames.Where(name => !existing.Contains(name)))
{
    await database.CreateCollectionAsync(collectionName);
}

await CreateIndexesAsync(database);
await MigrateLegacyCollectionAsync(database, "AccountsData", new Dictionary<string, string>
{
    ["BankAccount"] = "Accounts",
    ["BankTransaction"] = "Transactions",
    ["ServiceRequest"] = "ServiceRequests"
});
await MigrateLegacyOffersAsync(database, "OffersData");
await UpsertFileByTypeAsync(database, "AccountsData.json", new Dictionary<string, string>
{
    ["BankAccount"] = "Accounts",
    ["BankTransaction"] = "Transactions",
    ["ServiceRequest"] = "ServiceRequests"
});
await UpsertOffersFileAsync(database, "OffersData.json");
await UpsertFileAsync(database.GetCollection<BsonDocument>("Users"), "UserData.json");
Console.WriteLine("Azure DocumentDB data loading complete.");

static async Task MigrateLegacyCollectionAsync(
    IMongoDatabase database,
    string legacyCollectionName,
    IReadOnlyDictionary<string, string> targetCollections)
{
    HashSet<string> existing = (await database.ListCollectionNamesAsync()).ToList().ToHashSet(StringComparer.Ordinal);
    if (!existing.Contains(legacyCollectionName))
    {
        return;
    }

    List<BsonDocument> documents = await database.GetCollection<BsonDocument>(legacyCollectionName)
        .Find(FilterDefinition<BsonDocument>.Empty)
        .ToListAsync();
    await UpsertDocumentsByTypeAsync(database, documents, targetCollections, legacyCollectionName);
}

static async Task MigrateLegacyOffersAsync(IMongoDatabase database, string legacyCollectionName)
{
    HashSet<string> existing = (await database.ListCollectionNamesAsync()).ToList().ToHashSet(StringComparer.Ordinal);
    if (!existing.Contains(legacyCollectionName))
    {
        return;
    }

    List<BsonDocument> documents = await database.GetCollection<BsonDocument>(legacyCollectionName)
        .Find(FilterDefinition<BsonDocument>.Empty)
        .ToListAsync();
    await UpsertOffersAsync(database.GetCollection<BsonDocument>("Offers"), documents, legacyCollectionName);
}

static async Task UpsertFileByTypeAsync(
    IMongoDatabase database,
    string fileName,
    IReadOnlyDictionary<string, string> targetCollections)
{
    string json = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, fileName));
    BsonArray items = BsonSerializer.Deserialize<BsonArray>(json);
    await UpsertDocumentsByTypeAsync(
        database,
        items.Select(item => item.AsBsonDocument).ToList(),
        targetCollections,
        fileName);
}

static async Task UpsertDocumentsByTypeAsync(
    IMongoDatabase database,
    IEnumerable<BsonDocument> documents,
    IReadOnlyDictionary<string, string> targetCollections,
    string sourceName)
{
    foreach (IGrouping<string, BsonDocument> group in documents.GroupBy(document =>
    {
        if (!document.TryGetValue("type", out BsonValue? type) || !type.IsString || !targetCollections.ContainsKey(type.AsString))
        {
            throw new InvalidDataException($"{sourceName} contains an unsupported document type.");
        }
        return type.AsString;
    }))
    {
        string collectionName = targetCollections[group.Key];
        await UpsertDocumentsAsync(database.GetCollection<BsonDocument>(collectionName), group, sourceName);
    }
}

static async Task UpsertOffersFileAsync(IMongoDatabase database, string fileName)
{
    string json = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, fileName));
    BsonArray items = BsonSerializer.Deserialize<BsonArray>(json);
    await UpsertOffersAsync(
        database.GetCollection<BsonDocument>("Offers"),
        items.Select(item => item.AsBsonDocument),
        fileName);
}

static async Task UpsertOffersAsync(
    IMongoCollection<BsonDocument> collection,
    IEnumerable<BsonDocument> sourceDocuments,
    string sourceName)
{
    List<BsonDocument> documents = sourceDocuments.ToList();
    Dictionary<string, List<BsonDocument>> termsByOffer = documents
        .Where(document => document.GetValue("type", "").AsString == "Term")
        .GroupBy(document => document["offerId"].AsString)
        .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.Ordinal);

    List<BsonDocument> offers = [];
    foreach (BsonDocument sourceOffer in documents.Where(document => document.GetValue("type", "").AsString == "Offer"))
    {
        BsonDocument offer = sourceOffer.DeepClone().AsBsonDocument;
        List<BsonDocument> terms = termsByOffer.GetValueOrDefault(offer["id"].AsString) ?? [];
        offer["terms"] = new BsonArray(terms.Select(term =>
        {
            BsonDocument embeddedTerm = term.DeepClone().AsBsonDocument;
            embeddedTerm.Remove("_id");
            embeddedTerm.Remove("vector");
            return embeddedTerm;
        }));
        offer["vector"] = CalculateAggregateVector(terms, sourceName);
        offers.Add(offer);
    }

    await UpsertDocumentsAsync(collection, offers, sourceName);
}

static BsonArray CalculateAggregateVector(IReadOnlyCollection<BsonDocument> terms, string sourceName)
{
    List<double[]> vectors = terms
        .Where(term => term.TryGetValue("vector", out BsonValue? vector) && vector.IsBsonArray)
        .Select(term => term["vector"].AsBsonArray.Select(value => value.ToDouble()).ToArray())
        .ToList();
    if (vectors.Count == 0)
    {
        throw new InvalidDataException($"{sourceName} contains an offer without term vectors.");
    }

    int dimensions = vectors[0].Length;
    if (vectors.Any(vector => vector.Length != dimensions))
    {
        throw new InvalidDataException($"{sourceName} contains term vectors with inconsistent dimensions.");
    }

    double[] average = new double[dimensions];
    foreach (double[] vector in vectors)
    {
        for (int index = 0; index < dimensions; index++)
        {
            average[index] += vector[index] / vectors.Count;
        }
    }

    double magnitude = Math.Sqrt(average.Sum(value => value * value));
    if (magnitude == 0)
    {
        throw new InvalidDataException($"{sourceName} contains an offer with a zero aggregate vector.");
    }
    return new BsonArray(average.Select(value => value / magnitude));
}

static async Task UpsertFileAsync(IMongoCollection<BsonDocument> collection, string fileName)
{
    string json = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, fileName));
    BsonArray items = BsonSerializer.Deserialize<BsonArray>(json);
    await UpsertDocumentsAsync(collection, items.Select(item => item.AsBsonDocument), fileName);
}

static async Task UpsertDocumentsAsync(
    IMongoCollection<BsonDocument> collection,
    IEnumerable<BsonDocument> documents,
    string sourceName)
{
    List<WriteModel<BsonDocument>> writes = [];
    foreach (BsonDocument document in documents)
    {
        if (!document.TryGetValue("id", out BsonValue? id) || !id.IsString)
        {
            throw new InvalidDataException($"{sourceName} contains a document without a string id.");
        }
        document["_id"] = id;
        writes.Add(new ReplaceOneModel<BsonDocument>(Builders<BsonDocument>.Filter.Eq("_id", id), document) { IsUpsert = true });
    }
    if (writes.Count > 0)
    {
        await collection.BulkWriteAsync(writes, new BulkWriteOptions { IsOrdered = true });
    }
    Console.WriteLine($"Upserted {writes.Count} documents from {sourceName} into {collection.CollectionNamespace.CollectionName}.");
}

static async Task CreateIndexesAsync(IMongoDatabase database)
{
    await database.GetCollection<BsonDocument>("Users").Indexes.CreateOneAsync(
        new CreateIndexModel<BsonDocument>(
            Builders<BsonDocument>.IndexKeys.Ascending("tenantId").Ascending("id"),
            new CreateIndexOptions { Name = "users_tenant_id", Unique = true }));

    await database.GetCollection<BsonDocument>("Accounts").Indexes.CreateManyAsync(
    [
        new CreateIndexModel<BsonDocument>(
            Builders<BsonDocument>.IndexKeys.Ascending("tenantId").Ascending("id"),
            new CreateIndexOptions { Name = "accounts_tenant_id", Unique = true }),
        new CreateIndexModel<BsonDocument>(
            Builders<BsonDocument>.IndexKeys.Ascending("tenantId").Ascending("userId"),
            new CreateIndexOptions { Name = "accounts_tenant_user" }),
        new CreateIndexModel<BsonDocument>(
            Builders<BsonDocument>.IndexKeys.Ascending("tenantId").Ascending("accountId"),
            new CreateIndexOptions { Name = "accounts_tenant_account", Unique = true })
    ]);

    await database.GetCollection<BsonDocument>("Transactions").Indexes.CreateOneAsync(
        new CreateIndexModel<BsonDocument>(
            Builders<BsonDocument>.IndexKeys.Ascending("tenantId").Ascending("accountId").Descending("transactionDateTime"),
            new CreateIndexOptions { Name = "transactions_tenant_account_time" }));
    await database.GetCollection<BsonDocument>("Transactions").Indexes.CreateOneAsync(
        new CreateIndexModel<BsonDocument>(
            Builders<BsonDocument>.IndexKeys.Ascending("tenantId").Ascending("id"),
            new CreateIndexOptions { Name = "transactions_tenant_id", Unique = true }));

    await database.GetCollection<BsonDocument>("ServiceRequests").Indexes.CreateManyAsync(
    [
        new CreateIndexModel<BsonDocument>(
            Builders<BsonDocument>.IndexKeys.Ascending("tenantId").Ascending("id"),
            new CreateIndexOptions { Name = "requests_tenant_id", Unique = true }),
        new CreateIndexModel<BsonDocument>(
            Builders<BsonDocument>.IndexKeys.Ascending("tenantId").Descending("requestedOn"),
            new CreateIndexOptions { Name = "requests_tenant_time" }),
        new CreateIndexModel<BsonDocument>(
            Builders<BsonDocument>.IndexKeys.Ascending("tenantId").Ascending("accountId").Descending("requestedOn"),
            new CreateIndexOptions { Name = "requests_tenant_account_time" }),
        new CreateIndexModel<BsonDocument>(
            Builders<BsonDocument>.IndexKeys.Ascending("tenantId").Ascending("userId").Descending("requestedOn"),
            new CreateIndexOptions { Name = "requests_tenant_user_time" })
    ]);

    await database.GetCollection<BsonDocument>("Offers").Indexes.CreateManyAsync(
    [
        new CreateIndexModel<BsonDocument>(
            Builders<BsonDocument>.IndexKeys.Ascending("tenantId").Ascending("id"),
            new CreateIndexOptions { Name = "offers_tenant_id", Unique = true }),
        new CreateIndexModel<BsonDocument>(
            Builders<BsonDocument>.IndexKeys.Ascending("tenantId").Ascending("accountType"),
            new CreateIndexOptions { Name = "offers_tenant_account_type" })
    ]);

    await database.GetCollection<BsonDocument>("ChatsData").Indexes.CreateManyAsync(
    [
        new CreateIndexModel<BsonDocument>(
            Builders<BsonDocument>.IndexKeys.Ascending("id"),
            new CreateIndexOptions { Name = "chat_id", Unique = true }),
        new CreateIndexModel<BsonDocument>(
            Builders<BsonDocument>.IndexKeys.Ascending("tenantId").Ascending("userId").Ascending("sessionId"),
            new CreateIndexOptions { Name = "chat_owner_session" }),
        new CreateIndexModel<BsonDocument>(
            Builders<BsonDocument>.IndexKeys.Ascending("tenantId").Ascending("userId").Descending("timeStamp"),
            new CreateIndexOptions<BsonDocument>
            {
                Name = "chat_sessions_owner_time",
                PartialFilterExpression = Builders<BsonDocument>.Filter.Eq("type", "Session")
            }),
        new CreateIndexModel<BsonDocument>(
            Builders<BsonDocument>.IndexKeys.Ascending("tenantId").Ascending("userId").Ascending("sessionId").Ascending("timeStamp"),
            new CreateIndexOptions<BsonDocument>
            {
                Name = "chat_messages_session_time",
                PartialFilterExpression = Builders<BsonDocument>.Filter.Eq("type", "Message")
            }),
        new CreateIndexModel<BsonDocument>(
            Builders<BsonDocument>.IndexKeys.Ascending("tenantId").Ascending("userId").Ascending("sessionId").Ascending("id"),
            new CreateIndexOptions<BsonDocument>
            {
                Name = "chat_debug_session_id",
                PartialFilterExpression = Builders<BsonDocument>.Filter.Eq("type", "DebugLog")
            })
    ]);

    await database.GetCollection<BsonDocument>("Checkpoints").Indexes.CreateOneAsync(
        new CreateIndexModel<BsonDocument>(
            Builders<BsonDocument>.IndexKeys
                .Ascending("thread_id")
                .Ascending("checkpoint_ns")
                .Descending("checkpoint_id"),
            new CreateIndexOptions { Name = "checkpoints_thread_namespace_id", Unique = true }));

    await database.GetCollection<BsonDocument>("CheckpointWrites").Indexes.CreateOneAsync(
        new CreateIndexModel<BsonDocument>(
            Builders<BsonDocument>.IndexKeys
                .Ascending("thread_id")
                .Ascending("checkpoint_ns")
                .Ascending("checkpoint_id")
                .Ascending("task_id")
                .Ascending("idx"),
            new CreateIndexOptions { Name = "checkpoint_writes_thread_namespace_id_task_idx", Unique = true }));

    await database.GetCollection<BsonDocument>("ChatHistory").Indexes.CreateManyAsync(
    [
        new CreateIndexModel<BsonDocument>(
            Builders<BsonDocument>.IndexKeys.Ascending("id"),
            new CreateIndexOptions { Name = "chat_history_id", Unique = true }),
        new CreateIndexModel<BsonDocument>(
            Builders<BsonDocument>.IndexKeys.Ascending("sessionId").Descending("timeStamp"),
            new CreateIndexOptions { Name = "chat_history_session_time" })
    ]);

    await database.GetCollection<BsonDocument>("Debug").Indexes.CreateOneAsync(
        new CreateIndexModel<BsonDocument>(
            Builders<BsonDocument>.IndexKeys.Ascending("id").Ascending("sessionId"),
            new CreateIndexOptions { Name = "debug_id_session", Unique = true }));

    BsonDocument command = new()
    {
        { "createIndexes", "Offers" },
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