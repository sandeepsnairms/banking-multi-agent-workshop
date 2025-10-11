using Azure.Identity;
using Banking.Models;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Fluent;
using Microsoft.Azure.Cosmos.Linq;
using Microsoft.Extensions.Logging;
using OpenAI;
using System.Diagnostics;
using System.Linq.Expressions;
using System.Text;
using System.Text.Json;
using Container = Microsoft.Azure.Cosmos.Container;
using PartitionKey = Microsoft.Azure.Cosmos.PartitionKey;

namespace Banking.Services
{
    public class BankingDataService
    {
        private readonly EmbeddingService _embeddingService;
        private readonly Container _accountData;
        private readonly Container _userData;
        private readonly Container _requestData;
        private readonly Container _offerData;

        private readonly Database _database;

        private readonly ILogger _logger;

        public bool IsInitialized { get; private set; }


        public BankingDataService(EmbeddingService embeddingService,
            Database database, Container accountData, Container userData, Container requestData, Container offerData, ILoggerFactory loggerFactory)
        {

            _database = database;
            _accountData = accountData;
            _userData = userData;
            _requestData = requestData;
            _offerData = offerData;
            _embeddingService = embeddingService;

            _logger = loggerFactory.CreateLogger<BankingDataService>();
                        
            _logger.LogInformation("Banking service initialized.");
        }

        public async Task<BankUser> GetUserAsync(string tenantId, string userId)
        {
            try
            {
                var partitionKey = GetUserDataFullPK(tenantId);

                return await _userData.ReadItemAsync<BankUser>(
                       id: userId,
                       partitionKey: partitionKey);
            }
            catch (CosmosException ex)
            {
                _logger.LogError("Error getting user: {Message}", ex.Message);
                return null;
            }
        }

        public async Task<List<BankAccount>> GetUserRegisteredAccountsAsync(string tenantId, string userId)
        {
            try
            {
                QueryDefinition query = new QueryDefinition("SELECT * FROM c WHERE c.type = @type and c.userId=@userId")
                     .WithParameter("@type", nameof(BankAccount))
                     .WithParameter("@userId", userId);

                var partitionKey = GetAccountsPartialPK(tenantId);
                FeedIterator<BankAccount> response = _accountData.GetItemQueryIterator<BankAccount>(query, null, new QueryRequestOptions() { PartitionKey = partitionKey });

                List<BankAccount> output = new();
                while (response.HasMoreResults)
                {
                    FeedResponse<BankAccount> results = await response.ReadNextAsync();
                    output.AddRange(results);
                }

                return output;
            }
            catch (CosmosException ex)
            {
                _logger.LogError("Error getting user registered accounts: {Message}", ex.Message);
                return null;
            }
        }

        public async Task<BankAccount> GetAccountDetailsAsync(string tenantId, string userId, string accountId)
        {
            try
            {
                var partitionKey = GetAccountsDataFullPK(tenantId, accountId);

                return await _accountData.ReadItemAsync<BankAccount>(
                       id: accountId,
                       partitionKey: partitionKey);
            }
            catch (CosmosException ex)
            {
                _logger.LogError("Error getting account details: {Message}", ex.Message);
                return null;
            }
        }

        public async Task<List<BankTransaction>> GetTransactionsAsync(string tenantId, string accountId, DateTime startDate, DateTime endDate)
        {
            try
            {
                var partitionKey = GetAccountsDataFullPK(tenantId, accountId);

                QueryDefinition queryDefinition = new QueryDefinition(
                       "SELECT * FROM c WHERE c.accountId = @accountId AND c.transactionDateTime >= @startDate AND c.transactionDateTime <= @endDate AND c.type = @type")
                       .WithParameter("@accountId", accountId)
                       .WithParameter("@type", nameof(BankTransaction))
                       .WithParameter("@startDate", startDate)
                       .WithParameter("@endDate", endDate);

                List<BankTransaction> transactions = new List<BankTransaction>();
                using (FeedIterator<BankTransaction> feedIterator = _accountData.GetItemQueryIterator<BankTransaction>(queryDefinition, requestOptions: new QueryRequestOptions { PartitionKey = partitionKey }))
                {
                    while (feedIterator.HasMoreResults)
                    {
                        FeedResponse<BankTransaction> response = await feedIterator.ReadNextAsync();
                        transactions.AddRange(response);
                    }
                }

                return transactions;
            }
            catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                _logger.LogError("Error getting transactions: {Message}", ex.Message);
                return new List<BankTransaction>();
            }
        }

        public async Task<ServiceRequest> CreateFundTransferRequestAsync(string tenantId, string accountId, string userId, string requestAnnotation, string recipientEmail, string recipientPhone, decimal debitAmount)
        {
            var req = new ServiceRequest(ServiceRequestType.FundTransfer, tenantId, accountId, userId, requestAnnotation, recipientEmail, recipientPhone, debitAmount, DateTime.MinValue, null);
            return await AddServiceRequestAsync(req);
        }

        public async Task<ServiceRequest> CreateTeleBankerRequestAsync(string tenantId, string accountId, string userId, string requestAnnotation, DateTime scheduledDateTime)
        {
            var req = new ServiceRequest(ServiceRequestType.TeleBankerCallBack, tenantId, accountId, userId, requestAnnotation, string.Empty, string.Empty, 0, scheduledDateTime, null);
            return await AddServiceRequestAsync(req);
        }

        public Task<string> GetTeleBankerAvailabilityAsync()
        {
            return Task.FromResult("Monday to Friday, 8 AM to 8 PM Pacific Time");
        }

        public async Task<ServiceRequest> CreateComplaintAsync(string tenantId, string accountId, string userId, string requestAnnotation)
        {
            var req = new ServiceRequest(ServiceRequestType.Complaint, tenantId, accountId, userId, requestAnnotation, string.Empty, string.Empty, 0, DateTime.MinValue, null);
            return await AddServiceRequestAsync(req);
        }

        public async Task<ServiceRequest> CreateFulfilmentRequestAsync(string tenantId, string accountId, string userId, string requestAnnotation, Dictionary<string, string> fulfilmentDetails)
        {
            var req = new ServiceRequest(ServiceRequestType.Fulfilment, tenantId, accountId, userId, requestAnnotation, string.Empty, string.Empty, 0, DateTime.MinValue, fulfilmentDetails);
            return await AddServiceRequestAsync(req);
        }

        private async Task<ServiceRequest> AddServiceRequestAsync(ServiceRequest req)
        {
            try
            {
                var partitionKey = GetAccountsDataFullPK(req.TenantId, req.AccountId);
                ItemResponse<ServiceRequest> response = await _accountData.CreateItemAsync(req, partitionKey);
                return response.Resource;
            }
            catch (CosmosException ex)
            {
                _logger.LogError("Error adding service request: {Message}", ex.Message);
                return null;
            }
        }

        public async Task<List<ServiceRequest>> GetServiceRequestsAsync(string tenantId, string accountId, string? userId = null, ServiceRequestType? SRType = null)
        {
            try
            {
                var partitionKey = GetAccountsDataFullPK(tenantId, accountId);

                var queryBuilder = new StringBuilder("SELECT * FROM c WHERE c.type = @type");
                var queryDefinition = new QueryDefinition(queryBuilder.ToString())
                      .WithParameter("@type", nameof(ServiceRequest));

                if (!string.IsNullOrEmpty(userId))
                {
                    queryBuilder.Append(" AND c.userId = @userId");
                    queryDefinition = queryDefinition.WithParameter("@userId", userId);
                }

                if (SRType.HasValue)
                {
                    queryBuilder.Append(" AND c.SRType = @SRType");
                    queryDefinition = queryDefinition.WithParameter("@SRType", SRType);
                }

                List<ServiceRequest> reqs = new List<ServiceRequest>();
                using (FeedIterator<ServiceRequest> feedIterator = _requestData.GetItemQueryIterator<ServiceRequest>(queryDefinition, requestOptions: new QueryRequestOptions { PartitionKey = partitionKey }))
                {
                    while (feedIterator.HasMoreResults)
                    {
                        FeedResponse<ServiceRequest> response = await feedIterator.ReadNextAsync();
                        reqs.AddRange(response);
                    }
                }

                return reqs;
            }
            catch (CosmosException ex)
            {
                _logger.LogError("Error getting service requests: {Message}", ex.Message);
                return new List<ServiceRequest>();
            }
        }

        public async Task<bool> AddServiceRequestDescriptionAsync(string tenantId, string accountId, string requestId, string annotationToAdd)
        {
            try
            {
                var partitionKey = GetAccountsDataFullPK(tenantId, accountId);

                var patchOperations = new List<PatchOperation>
                {
                    PatchOperation.Add("/requestAnnotations/-", $"[{DateTime.Now.ToUniversalTime().ToString()}] : {annotationToAdd}")
                };

                ItemResponse<ServiceRequest> response = await _requestData.PatchItemAsync<ServiceRequest>(
                    id: requestId,
                    partitionKey: partitionKey,
                    patchOperations: patchOperations
                );

                return true;
            }
            catch (CosmosException ex)
            {
                _logger.LogError("Error adding service request description: {Message}", ex.Message);
                return false;
            }
        }

        public async Task<List<OfferTerm>> SearchOfferTermsAsync(string tenantId, AccountType accountType, string requirementDescription)
        {
            try
            {
                // Generate embeddings for the requirement description
                var queryVector = await _embeddingService.GenerateEmbeddingAsync(requirementDescription);


                // Build the vector search query with filters
                var query = new QueryDefinition(@"
                            SELECT 
                                c.id, 
                                c.tenantId, 
                                c.offerId, 
                                c.name, 
                                c.text, 
                                c.type, 
                                c.accountType, 
                                VectorDistance(c.vector, @queryVector) AS similarityScore
                            FROM c 
                            WHERE c.type = @type 
                              AND c.tenantId = @tenantId 
                              AND c.accountType = @accountType
                            ORDER BY VectorDistance(c.vector, @queryVector)
                        ")
                .WithParameter("@queryVector", queryVector)
                .WithParameter("@type", "Term")
                .WithParameter("@tenantId", tenantId)
                .WithParameter("@accountType", accountType.ToString());

                // Define partition key
                var partitionKey = new PartitionKey(tenantId);

                // Run query
                var offerTerms = new List<OfferTerm>();
                using (FeedIterator<OfferTerm> feedIterator = _offerData.GetItemQueryIterator<OfferTerm>(
                    query,
                    requestOptions: new QueryRequestOptions
                    {
                        PartitionKey = partitionKey,
                        MaxItemCount = 10 // Limit for performance
                    }))
                {
                    while (feedIterator.HasMoreResults)
                    {
                        FeedResponse<OfferTerm> response = await feedIterator.ReadNextAsync();
                        offerTerms.AddRange(response);
                    }
                }

                return offerTerms;


            }
            catch (Exception ex)
            {
                _logger.LogError("Error searching offer terms: {Message}", ex.Message);
                return new List<OfferTerm>();
            }
        }

        public async Task<Offer> GetOfferDetailsAsync(string tenantId, string offerId)
        {
            try
            {
                var partitionKey = new PartitionKey(tenantId);

                return await _offerData.ReadItemAsync<Offer>(
                       id: offerId,
                       partitionKey: new PartitionKey(tenantId));
            }
            catch (CosmosException ex)
            {
                _logger.LogError("Error getting offer details: {Message}", ex.Message);
                return null;
            }
        }

        // Helper methods to replace PartitionManager calls (these need proper implementation)
        private static PartitionKey GetUserDataFullPK(string tenantId)
        {
            // TODO: Implement proper partition key logic or inject PartitionManager
            return new PartitionKey(tenantId);
        }

        private static PartitionKey GetAccountsPartialPK(string tenantId)
        {
            // TODO: Implement proper partition key logic or inject PartitionManager
            return new PartitionKey(tenantId);
        }

        private static PartitionKey GetAccountsDataFullPK(string tenantId, string accountId)
        {
            // TODO: Implement proper partition key logic or inject PartitionManager
            return new PartitionKey(tenantId);
        }
    }
}