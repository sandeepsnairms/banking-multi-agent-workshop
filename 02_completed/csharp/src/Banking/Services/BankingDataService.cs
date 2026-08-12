using Banking.Models;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Banking.Services;

public class BankingDataService
{
    private readonly EmbeddingService _embeddingService;
    private readonly IMongoCollection<BsonDocument> _accountData;
    private readonly IMongoCollection<BsonDocument> _transactionData;
    private readonly IMongoCollection<BsonDocument> _userData;
    private readonly IMongoCollection<BsonDocument> _requestData;
    private readonly IMongoCollection<BsonDocument> _offerData;
    private readonly ILogger _logger;

    public bool IsInitialized { get; private set; }

    public BankingDataService(
        EmbeddingService embeddingService,
        IMongoDatabase database,
        IMongoCollection<BsonDocument> accountData,
        IMongoCollection<BsonDocument> transactionData,
        IMongoCollection<BsonDocument> userData,
        IMongoCollection<BsonDocument> requestData,
        IMongoCollection<BsonDocument> offerData,
        ILoggerFactory loggerFactory)
    {
        _ = database;
        _accountData = accountData;
        _transactionData = transactionData;
        _userData = userData;
        _requestData = requestData;
        _offerData = offerData;
        _embeddingService = embeddingService;
        _logger = loggerFactory.CreateLogger<BankingDataService>();
        _logger.LogInformation("Banking service initialized.");
    }

    public async Task<BankUser?> GetUserAsync(string tenantId, string userId)
    {
        try
        {
            BsonDocument? document = await _userData.Find(FilterById(tenantId, userId)).FirstOrDefaultAsync();
            return document is null ? null : DocumentDbSerialization.FromDocument<BankUser>(document);
        }
        catch (MongoException ex)
        {
            _logger.LogError(ex, "Error getting user.");
            return null;
        }
    }

    public async Task<List<BankAccount>> GetUserRegisteredAccountsAsync(string tenantId, string userId)
    {
        try
        {
            FilterDefinition<BsonDocument> filter = Builders<BsonDocument>.Filter.And(
                Builders<BsonDocument>.Filter.Eq("tenantId", tenantId),
                Builders<BsonDocument>.Filter.Eq("type", nameof(BankAccount)),
                Builders<BsonDocument>.Filter.Eq("userId", userId));
            return Convert<BankAccount>(await _accountData.Find(filter).ToListAsync());
        }
        catch (MongoException ex)
        {
            _logger.LogError(ex, "Error getting user registered accounts.");
            return [];
        }
    }

    public async Task<BankAccount?> GetAccountDetailsAsync(string tenantId, string userId, string accountId)
    {
        try
        {
            FilterDefinition<BsonDocument> filter = Builders<BsonDocument>.Filter.And(
                FilterById(tenantId, accountId),
                Builders<BsonDocument>.Filter.Eq("userId", userId));
            BsonDocument? document = await _accountData.Find(filter).FirstOrDefaultAsync();
            return document is null ? null : DocumentDbSerialization.FromDocument<BankAccount>(document);
        }
        catch (MongoException ex)
        {
            _logger.LogError(ex, "Error getting account details.");
            return null;
        }
    }

    public async Task<List<BankTransaction>> GetTransactionsAsync(string tenantId, string accountId, DateTime startDate, DateTime endDate)
    {
        try
        {
            FilterDefinition<BsonDocument> filter = Builders<BsonDocument>.Filter.And(
                Builders<BsonDocument>.Filter.Eq("tenantId", tenantId),
                Builders<BsonDocument>.Filter.Eq("accountId", accountId),
                Builders<BsonDocument>.Filter.Gte("transactionDateTime", startDate.ToUniversalTime().ToString("O")),
                Builders<BsonDocument>.Filter.Lte("transactionDateTime", endDate.ToUniversalTime().ToString("O")));
            return Convert<BankTransaction>(await _transactionData.Find(filter)
                .Sort(Builders<BsonDocument>.Sort.Ascending("transactionDateTime")).ToListAsync());
        }
        catch (MongoException ex)
        {
            _logger.LogError(ex, "Error getting transactions.");
            return [];
        }
    }

    public Task<ServiceRequest> CreateFundTransferRequestAsync(string tenantId, string accountId, string userId, string requestAnnotation, string recipientEmail, string recipientPhone, decimal debitAmount) =>
        AddServiceRequestAsync(new ServiceRequest(ServiceRequestType.FundTransfer, tenantId, accountId, userId, requestAnnotation, recipientEmail, recipientPhone, debitAmount, DateTime.MinValue, null));

    public Task<ServiceRequest> CreateTeleBankerRequestAsync(string tenantId, string accountId, string userId, string requestAnnotation, DateTime scheduledDateTime) =>
        AddServiceRequestAsync(new ServiceRequest(ServiceRequestType.TeleBankerCallBack, tenantId, accountId, userId, requestAnnotation, string.Empty, string.Empty, 0, scheduledDateTime, null));

    public Task<string> GetTeleBankerAvailabilityAsync() => Task.FromResult("Monday to Friday, 8 AM to 8 PM Pacific Time");

    public Task<ServiceRequest> CreateComplaintAsync(string tenantId, string accountId, string userId, string requestAnnotation) =>
        AddServiceRequestAsync(new ServiceRequest(ServiceRequestType.Complaint, tenantId, accountId, userId, requestAnnotation, string.Empty, string.Empty, 0, DateTime.MinValue, null));

    public Task<ServiceRequest> CreateFulfilmentRequestAsync(string tenantId, string accountId, string userId, string requestAnnotation, Dictionary<string, string> fulfilmentDetails) =>
        AddServiceRequestAsync(new ServiceRequest(ServiceRequestType.Fulfilment, tenantId, accountId, userId, requestAnnotation, string.Empty, string.Empty, 0, DateTime.MinValue, fulfilmentDetails));

    private async Task<ServiceRequest> AddServiceRequestAsync(ServiceRequest request)
    {
        await _requestData.InsertOneAsync(DocumentDbSerialization.ToDocument(request));
        return request;
    }

    public async Task<List<ServiceRequest>> GetServiceRequestsAsync(string tenantId, string accountId, string? userId = null, ServiceRequestType? SRType = null)
    {
        try
        {
            List<FilterDefinition<BsonDocument>> filters =
            [
                Builders<BsonDocument>.Filter.Eq("tenantId", tenantId),
                Builders<BsonDocument>.Filter.Eq("accountId", accountId)
            ];
            if (!string.IsNullOrWhiteSpace(userId))
            {
                filters.Add(Builders<BsonDocument>.Filter.Eq("userId", userId));
            }
            if (SRType.HasValue)
            {
                filters.Add(Builders<BsonDocument>.Filter.Eq("srType", SRType.Value.ToString()));
            }
            return Convert<ServiceRequest>(await _requestData.Find(Builders<BsonDocument>.Filter.And(filters)).ToListAsync());
        }
        catch (MongoException ex)
        {
            _logger.LogError(ex, "Error getting service requests.");
            return [];
        }
    }

    public async Task<bool> AddServiceRequestDescriptionAsync(string tenantId, string accountId, string requestId, string annotationToAdd)
    {
        try
        {
            FilterDefinition<BsonDocument> filter = Builders<BsonDocument>.Filter.And(
                FilterById(tenantId, requestId),
                Builders<BsonDocument>.Filter.Eq("accountId", accountId));
            UpdateResult result = await _requestData.UpdateOneAsync(filter,
                Builders<BsonDocument>.Update.Push("requestAnnotations", $"[{DateTime.UtcNow}] : {annotationToAdd}"));
            return result.ModifiedCount == 1;
        }
        catch (MongoException ex)
        {
            _logger.LogError(ex, "Error adding service request description.");
            return false;
        }
    }

    public async Task<List<string>> SearchOfferTermsAsync(string tenantId, AccountType accountType, string requirementDescription)
    {
        try
        {
            ReadOnlyMemory<float> queryVector = await _embeddingService.GenerateEmbeddingAsync(requirementDescription);
            BsonArray vector = new(queryVector.Span.ToArray().Select(value => (BsonValue)value));
            BsonDocument search = new("$search", new BsonDocument
            {
                { "cosmosSearch", new BsonDocument
                    {
                        { "vector", vector },
                        { "path", "vector" },
                        { "k", 10 }
                    }
                },
                { "returnStoredSource", true }
            });
            List<BsonDocument> documents = await _offerData
                .Aggregate<BsonDocument>(new[]
                {
                    search,
                    new BsonDocument("$match", new BsonDocument
                    {
                        { "tenantId", tenantId },
                        { "accountType", accountType.ToString() }
                    }),
                    new BsonDocument("$limit", 10),
                    new BsonDocument("$unwind", "$terms"),
                    new BsonDocument("$project", new BsonDocument
                    {
                        { "_id", 0 },
                        { "term", "$terms" }
                    }),
                    new BsonDocument("$limit", 10)
                })
                .ToListAsync();
            return documents
                .Where(document => document.TryGetValue("term", out BsonValue? term) && term.IsString)
                .Select(document => document["term"].AsString)
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching offer terms.");
            return [];
        }
    }

    public async Task<Offer?> GetOfferDetailsAsync(string tenantId, string offerId)
    {
        try
        {
            BsonDocument? document = await _offerData.Find(FilterById(tenantId, offerId)).FirstOrDefaultAsync();
            return document is null ? null : DocumentDbSerialization.FromDocument<Offer>(document);
        }
        catch (MongoException ex)
        {
            _logger.LogError(ex, "Error getting offer details.");
            return null;
        }
    }

    private static FilterDefinition<BsonDocument> FilterById(string tenantId, string id) =>
        Builders<BsonDocument>.Filter.And(
            Builders<BsonDocument>.Filter.Eq("tenantId", tenantId),
            Builders<BsonDocument>.Filter.Eq("id", id));

    private static List<T> Convert<T>(IEnumerable<BsonDocument> documents) =>
        documents.Select(DocumentDbSerialization.FromDocument<T>).ToList();
}